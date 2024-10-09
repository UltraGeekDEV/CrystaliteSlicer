using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Remote.Protocol.Input;
using Crystalite.Utils;
using System;
using System.Diagnostics;
using System.Numerics;

namespace Crystalite;

public partial class _3DViewPort : UserControl
{
    private bool mmbWasPressed = false;
    private Point mmbPoint;

    private bool rmbWasPressed = false;
    private Point rmbPoint;

    public _3DViewPort()
    {
        InitializeComponent();
    }

    public void OpenGLViewPort_PointerPressed(object sender, PointerPressedEventArgs args)
    {
        Debug.WriteLine("Detected MouseDown");
        var point = args.GetCurrentPoint(sender as Control);
        if (point.Properties.IsMiddleButtonPressed)
        {
            mmbWasPressed = true;
            mmbPoint = point.Position;
        }
        if (point.Properties.IsRightButtonPressed)
        {
            rmbWasPressed = true;
            rmbPoint = point.Position;
        }
    }
    public void OpenGLViewPort_PointerReleased(object sender, PointerReleasedEventArgs args)
    {
        Debug.WriteLine("Detected MouseUp");
        var point = args.GetCurrentPoint(sender as Control);
        mmbWasPressed = point.Properties.IsMiddleButtonPressed;
        rmbWasPressed = point.Properties.IsRightButtonPressed;
    }

    public void OpenGLViewPort_PointerMoved(object sender, PointerEventArgs args)
    {
        
        if (mmbWasPressed)
        {
            var cur = args.GetPosition(sender as Control);
            var dir = (mmbPoint - cur)/Bounds.Width *CameraData.instance.sensitivity;
            Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, CameraData.instance.Forward));
            CameraData.instance.targetPos.Y -= (float)dir.Y;
            CameraData.instance.targetPos += right*(float)dir.X;
            mmbPoint = cur;
        }
        if (rmbWasPressed)
        {
            var cur = args.GetPosition(sender as Control);
            var dir = (rmbPoint - cur) / Bounds.Width * CameraData.instance.sensitivity;
            CameraData.instance.eulerAngles.Y += (float)dir.X *72;
            CameraData.instance.eulerAngles.X -= (float)dir.Y *72;
            CameraData.instance.eulerAngles.X = Math.Clamp(CameraData.instance.eulerAngles.X, -85, 85);
            rmbPoint = cur;
        }
    }
    public void OpenGLViewPort_PointerWheelChanged(object sender, PointerWheelEventArgs args)
    {
        var change = args.Delta;
        CameraData.instance.dist -= (float)change.Y;
        CameraData.instance.dist = MathF.Max(CameraData.instance.dist, 0.01f);
    }
}