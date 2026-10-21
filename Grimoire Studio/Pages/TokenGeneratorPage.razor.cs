using SkiaSharp;

namespace Grimoire_Studio.Pages
{
    public partial class TokenGeneratorPage
    {
        // Stan kontrolek
        private string SelectedPreset = "";
        private string TokenType = "Gracz";
        private string SelectedFont = "Times New Roman";
        private string LabelText = "Placeholder";
        private int FontSize = 40;
        private double TextVertical = 0;
        private double LetterSpacing = 0;

        private string DescriptionText = "opis";
        private string DescriptionFont = "Times New Roman";
        private int DescriptionFontSize = 20;
        private double DescriptionVertical = -0.25;

        private double TokenSizeCm = 5.0;

        private double ImageScale = 1.0;
        private double ImageVertical = 0;
        private double ImageHorizontal = 0;

        private string BorderColor = "#1E90FF";
        private string BorderColor2 = "#000000";
        private int BorderThickness = 20;

        private bool LeafRightCheck = false;
        private bool LeafLeftCheck = false;
        private bool LeafOrangeCheck = false;
        private bool LeafTopCheck = false;
        private int LeafTopCount = 1;
        private double LeafScale = 1.0;

        private int PreviewSize = 500;
        private string? PreviewImageSrc = null;

        // Lista dostępnych czcionek
        public List<string> AvailableFonts { get; set; } = new()
        {
            "Times New Roman",
            "Georgia",
            "Segoe UI",
            "Arial",
            "Calibri",
            "Poetsen One",
            "KG Red Hands",
            "Autour One",
            "Bellota",
            "Chopin Script",
            "Roboto"
        };

        protected override void OnInitialized()
        {
            UpdatePreview();
        }

        // Metoda generująca podgląd graficzny za pomocą SkiaSharp
        private void UpdatePreview()
        {
            int width = PreviewSize;
            int height = PreviewSize;

            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            // Rysowanie bazowego koła tokena
            var center = new SKPoint(width / 2f, height / 2f);
            float radius = (Math.Min(width, height) / 2f) - (BorderThickness / 2f);

            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = BorderThickness,
                Color = SKColor.Parse(BorderColor)
            };

            // Wypełnienie tła tokena
            using var fillPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = SKColors.White
            };
            canvas.DrawCircle(center, radius, fillPaint);

            // Obwódka
            canvas.DrawCircle(center, radius, paint);

            // Rysowanie głównego tekstu (Nazwa) z użyciem nowszego API SkiaSharp
            using var textPaint = new SKPaint
            {
                IsAntialias = true,
                Color = SKColors.Black
            };

            using var font = new SKFont
            {
                Size = FontSize
            };

            // Pozycja Y tekstu z uwzględnieniem przesunięcia pionowego
            float textY = (float)(center.Y + (FontSize / 3f) + (TextVertical * 50));

            // Wyrównanie do środka przekazujemy bezpośrednio jako argument DrawText
            canvas.DrawText(LabelText, center.X, textY, SKTextAlign.Center, font, textPaint);

            // Konwersja do Base64 w formacie PNG
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            PreviewImageSrc = $"data:image/png;base64,{Convert.ToBase64String(data.ToArray())}";
            StateHasChanged();
        }

        // Wywołanie odświeżenia przy każdej zmianie parametrów
        private void OnParametersChanged()
        {
            UpdatePreview();
        }

        private void SavePreset_Click() { }
        private void DeletePreset_Click() { }
        private void AddToSheet_Click() { }
        private void ExportPdf_Click() { }
        private void ChooseBackgroundButton_Click() { }
        private void ChooseImageButton_Click() { }
        private void ClearImageButton_Click() { }
        private void GenerateButton_Click()
        {
            UpdatePreview();
        }
    }
}