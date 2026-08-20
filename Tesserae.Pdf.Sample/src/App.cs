using static Tesserae.UI;
using static Transpose.Core.dom;

namespace Tesserae.Pdf.Sample
{
    public static class App
    {
        public static void Main()
        {
            document.body.appendChild(TextBlock("Tesserae.Pdf sample").Render());
        }
    }
}
