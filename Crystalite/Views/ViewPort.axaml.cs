using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;

namespace Crystalite
{

    //public partial class ViewPort : UserControl
    //{
    //    public ViewPort()
    //    {
    //        InitializeComponent();
    //    }
    //}

    public class OpenGLViewPort : OpenGlControlBase
    {
        private int vertexShader;
        private int fragmentShader;
        private int shaderProgram;
        private int vertexBufferObject;
        private int indexBufferObject;
        private int vertexArrayObject;

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            throw new System.NotImplementedException();
        }
    }
}