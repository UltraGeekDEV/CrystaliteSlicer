using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Remote.Protocol.Input;
using Avalonia.VisualTree;
using Crystalite.Utils;
using OpenTK.Windowing.Common.Input;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Crystalite;

public partial class _3DViewPort : UserControl
{
    private bool mmbWasPressed = false;
    private Point mmbPoint;

    private bool rmbWasPressed = false;
    private Point rmbPoint;

    private bool lmbWasPressed = false;
    private Point lmbPoint;

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    private struct POINT
    {
        public int x;
        public int y;
    }

    POINT curPoint;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    private void SetPointerPosition(int x, int y)
    {
        SetCursorPos(x, y);
    }

    private void GetPointerPosition()
    {
        GetCursorPos(out curPoint);
    }

    public _3DViewPort()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OpenGLViewPort_PointerPressed, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OpenGLViewPort_PointerReleased, handledEventsToo: true);
    }

    public void OpenGLViewPort_PointerPressed(object sender, PointerPressedEventArgs args)
    {
        Debug.WriteLine("Detected MouseDown");
        var point = args.GetCurrentPoint(sender as Control);
        if (point.Properties.IsMiddleButtonPressed && !mmbWasPressed)
        {
            mmbWasPressed = true;
            mmbPoint = point.Position;
        }
        if (point.Properties.IsRightButtonPressed && !rmbWasPressed)
        {
            rmbWasPressed = true;
            rmbPoint = point.Position;
        }
        if (point.Properties.IsLeftButtonPressed && !lmbWasPressed)
        {
            GetPointerPosition();
            lmbWasPressed = true;
            lmbPoint = point.Position;
            var view = this.FindDescendantOfType<OpenGLViewPort>();

            var width = view.Bounds.Width;
            var height = view.Bounds.Height;

            float NDCx = (2.0f * (float)point.Position.X) / (float)width - 1.0f;
            float NDCy = 1.0f - (2.0f * (float)point.Position.Y) / (float)height;
            float NDCz = 1.0f;
            var rayCLip = new Vector3(NDCx, NDCy, NDCz);

            Matrix4x4 inverseProjection;
            Matrix4x4.Invert(Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, (float)(width / height), 0.01f, 1000f), out inverseProjection);
            var rayView = Vector3.Transform(rayCLip, inverseProjection);
            rayView.Z = -1;

            Matrix4x4 inverseView;
            Matrix4x4.Invert(CameraData.CreateViewMatrix(), out inverseView);
            var rayWorld = Vector3.Transform(rayView, inverseView);
            var start = Vector3.Zero;
            start = Vector3.Transform(start, inverseView);
            MeshData.instance.CastRay(Vector3.Normalize(rayWorld-start), start);
        }
    }
    public void OpenGLViewPort_PointerReleased(object sender, PointerReleasedEventArgs args)
    {
        Debug.WriteLine("Detected MouseUp");
        var point = args.GetCurrentPoint(sender as Control);
        mmbWasPressed = point.Properties.IsMiddleButtonPressed;
        rmbWasPressed = point.Properties.IsRightButtonPressed;
        if (lmbWasPressed && !point.Properties.IsLeftButtonPressed)
        {
            lmbWasPressed = false;
            Cursor = Cursor.Default;
            MeshData.instance.ReleaseAxis();
        }
    }

    public void OpenGLViewPort_PointerMoved(object sender, PointerEventArgs args)
    {
        
        if (mmbWasPressed)
        {
            var cur = args.GetPosition(sender as Control);
            var dir = (mmbPoint - cur)/Bounds.Width * CameraData.instance.sensitivity * 10;
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
        if (lmbWasPressed && MeshData.instance.activeAxis != null)
        {
            var view = this.FindDescendantOfType<OpenGLViewPort>();

            var width = view.Bounds.Width;
            var height = view.Bounds.Height;

            var perspective = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, (float)(width / height), 0.01f, 1000f);

            var cur = args.GetPosition(sender as Control);
            var dir = (lmbPoint-cur);
            var dirVec = new Vector3((float)-dir.X / (float)width, (float)dir.Y / (float)height, 0)*20;
            var zero = Vector3.Transform(Vector3.Transform(Vector3.Zero, perspective),  CameraData.CreateViewMatrix());
            var axisDir = Vector3.Normalize(Vector3.Transform(Vector3.Transform(MeshData.instance.activeAxis.axis, perspective),  CameraData.CreateViewMatrix())-zero);

            MeshData.instance.selected.translation.Translation += MeshData.instance.activeAxis.axis*Vector3.Dot(axisDir,dirVec)*new Vector3(1.0f,1.0f,-1.0f);
            MeshData.instance.UpdateTranslationHandles();
            Cursor = new Cursor(StandardCursorType.None);
            SetCursorPos(curPoint.x, curPoint.y);
            
        }
    }
    public void OpenGLViewPort_PointerWheelChanged(object sender, PointerWheelEventArgs args)
    {
        var change = args.Delta;
        CameraData.instance.dist -= (float)change.Y*5;
        CameraData.instance.dist = MathF.Max(CameraData.instance.dist, 0.01f);
    }
}