using System;

using Foundation;
using AppKit;

namespace Eddie.UI.Cocoa.Osx
{
    public partial class WindowSplash : NSWindow
    {
#if EDDIE_DOTNET
        public WindowSplash(ObjCRuntime.NativeHandle handle) : base(handle)
#else
        public WindowSplash(IntPtr handle) : base(handle)
#endif
        {
        }

        [Export("initWithCoder:")]
        public WindowSplash(NSCoder coder) : base(coder)
        {
        }

        public override void AwakeFromNib()
        {
            base.AwakeFromNib();
        }
    }
}
