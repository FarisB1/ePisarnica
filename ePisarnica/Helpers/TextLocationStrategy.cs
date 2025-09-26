using iTextSharp.text.pdf.parser;

namespace ePisarnica.Helpers
{
    public class TextLocationStrategy : LocationTextExtractionStrategy
    {
        public float MinY { get; private set; } = float.MaxValue;
        public float MaxX { get; private set; } = 0;

        public override void RenderText(TextRenderInfo renderInfo)
        {
            string text = renderInfo.GetText()?.Trim();

            // Skip page numbers (pure digits or digit/digit style)
            if (string.IsNullOrEmpty(text) ||
                System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+([/]\d+)?$"))
            {
                return;
            }

            base.RenderText(renderInfo);

            var bottomLeft = renderInfo.GetDescentLine().GetStartPoint();
            var topRight = renderInfo.GetAscentLine().GetEndPoint();

            float y = bottomLeft[Vector.I2];
            float x = topRight[Vector.I1];

            if (y < MinY) MinY = y;
            if (x > MaxX) MaxX = x;
        }

        public override void RenderImage(ImageRenderInfo renderInfo)
        {
            var ctm = renderInfo.GetImageCTM(); // Current transformation matrix
            var rect = new System.Drawing.RectangleF(
                ctm[6], // X
                ctm[7], // Y
                ctm[0], // width
                ctm[4]  // height
            );

            float y = rect.Y;
            float x = rect.Right;

            if (y < MinY) MinY = y;
            if (x > MaxX) MaxX = x;

            base.RenderImage(renderInfo);
        }
    }
}
