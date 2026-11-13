// <eddie_source_header>
// This file is part of Eddie/AirVPN software.
// Copyright (C)2014-2026 AirVPN (support@airvpn.org) / https://airvpn.org )
//
// Eddie is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Eddie is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Eddie. If not, see <http://www.gnu.org/licenses/>.
// </eddie_source_header>

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using Foundation;
using AppKit;
using Eddie.Core;
using System.Runtime.InteropServices;

namespace Eddie.UI.Cocoa.Osx
{
	public class ProcessRootLauncher
	{
		const string SecurityFramework = "/System/Library/Frameworks/Security.framework/Versions/Current/Security";

		public static bool LaunchExternalTool(string toolPath, string[] args)
		{
			IntPtr authReference = IntPtr.Zero;
			int result = AuthorizationCreate(IntPtr.Zero, IntPtr.Zero, 0, out authReference);
			if (result != 0)
			{
				Eddie.Core.Engine.Instance.Logs.LogFatal("Error while creating Auth Reference: " + result.ToString());
				return false;
			}

			// Remember: called program still have uid as normal user. Use setuid().

			// Intentional: legacy Cocoa UI elevated launch only; Eddie3 uses Platform.RunElevated. Retire with legacy UI.
			// The API requires a NULL-terminated argv. Mono's marshaller appends the terminator, .NET's does not.
			string[] argv = new string[args.Length + 1];
			Array.Copy(args, argv, args.Length);
#if EDDIE_DOTNET
			HashSet<int> childrenBefore = GetChildPids();
#endif
			result = AuthorizationExecuteWithPrivileges(authReference, toolPath, 0, argv, IntPtr.Zero);
#if EDDIE_DOTNET
			if (result == 0)
				ReapLaunchedChildren(childrenBefore, toolPath);
#endif
			int resultFree = AuthorizationFree(authReference, AuthorizationFlags.DestroyRights);
			return (result == 0);
		}

		public enum AuthorizationStatus
		{
			Success = 0,
			InvalidSet = -60001,
			InvalidRef = -60002,
			InvalidTag = -60003,
			InvalidPointer = -60004,
			Denied = -60005,
			Canceled = -60006,
			InteractionNotAllowed = -60007,
			Internal = -60008,
			ExternalizeNotAllowed = -60009,
			InternalizeNotAllowed = -60010,
			InvalidFlags = -60011,
			ToolExecuteFailure = -60031,
			ToolEnvironmentError = -60032,
			BadAddress = -60033,
		}

		[Flags]
		public enum AuthorizationFlags
		{
			Defaults,
			InteractionAllowed = 1 << 0,
			ExtendRights = 1 << 1,
			PartialRights = 1 << 2,
			DestroyRights = 1 << 3,
			PreAuthorize = 1 << 4,
		}

		//
		// For ease of use, we let the user pass the AuthorizationParameters, and we
		// create the structure for them with the proper data
		//
		public class AuthorizationParameters
		{
			public string PathToSystemPrivilegeTool;
			public string Prompt;
			public string IconPath;
		}

		public class AuthorizationEnvironment
		{
			public string Username;
			public string Password;
			public bool AddToSharedCredentialPool;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct AuthorizationItem
		{
			public IntPtr name;
			public IntPtr valueLen;
			public IntPtr value;
			public int flags;  // zero
		}

		[DllImport(SecurityFramework)]
		extern static int AuthorizationCreate(IntPtr autorizationRights,
											   IntPtr environment,
											   int authFlags,
											   out IntPtr authRef);

		[DllImport(SecurityFramework)]
		extern static int AuthorizationExecuteWithPrivileges(IntPtr authRef,
															  string pathToTool,
															  int authFlags,
															  string[] args,
															  IntPtr pipe);

		[DllImport(SecurityFramework)]
		extern static int AuthorizationFree(IntPtr handle, AuthorizationFlags flags);

#if EDDIE_DOTNET
		// AuthorizationExecuteWithPrivileges starts the tool as a direct child of this process
		// (security_authtrampoline, which then execs the tool), outside System.Diagnostics.Process.
		// .NET only reaps the children it started itself, so when the elevated helper exits it stays a zombie
		// and IElevated.Stop waits forever in Process.WaitForExit (shutdown stuck at "Exiting"). Mono reaped it.
		// Wait on exactly the children created by this call.
		const string AuthTrampolinePath = "/usr/libexec/security_authtrampoline";
		const int EINTR = 4;

		static void ReapLaunchedChildren(HashSet<int> childrenBefore, string toolPath)
		{
			foreach (int pid in GetChildPids())
			{
				if (childrenBefore.Contains(pid))
					continue;

				// Skip children started meanwhile by other threads through System.Diagnostics.Process:
				// .NET reaps those, and waiting on them here would take their exit status.
				string path = GetPidPath(pid);
				if ((path != AuthTrampolinePath) && (System.IO.Path.GetFileName(path) != System.IO.Path.GetFileName(toolPath)))
					continue;

				System.Threading.Thread reaper = new System.Threading.Thread(() =>
				{
					while ((waitpid(pid, out int _, 0) == -1) && (Marshal.GetLastPInvokeError() == EINTR))
					{
					}
				});
				reaper.IsBackground = true;
				reaper.Name = "Elevated reaper " + pid.ToString(System.Globalization.CultureInfo.InvariantCulture);
				reaper.Start();
			}
		}

		static HashSet<int> GetChildPids()
		{
			HashSet<int> pids = new HashSet<int>();
			int[] buffer = new int[4096];
			if (proc_listchildpids(Environment.ProcessId, buffer, buffer.Length * sizeof(int)) == -1)
				return pids;
			foreach (int pid in buffer)
				if (pid > 0)
					pids.Add(pid);
			return pids;
		}

		static string GetPidPath(int pid)
		{
			byte[] buffer = new byte[4096]; // PROC_PIDPATHINFO_MAXSIZE
			int len = proc_pidpath(pid, buffer, (uint)buffer.Length);
			if (len <= 0)
				return "";
			return System.Text.Encoding.UTF8.GetString(buffer, 0, len);
		}

		[DllImport("libc")]
		extern static int proc_listchildpids(int ppid, int[] buffer, int buffersize);

		[DllImport("libc")]
		extern static int proc_pidpath(int pid, byte[] buffer, uint buffersize);

		[DllImport("libc", SetLastError = true)]
		extern static int waitpid(int pid, out int status, int options);
#endif

	}
}

