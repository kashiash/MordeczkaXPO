using System.Drawing;
using DevExpress.Drawing;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.Editors;

namespace XafXPODynAssem.Module.Pomiar
{
    /// <summary>
    /// PROTOTYP DO WYRZUCENIA. Regula wygladu podawana silnikowi DevExpressa
    /// przez CollectAppearanceRules. Wzorzec z DataDrive (AdditionalAppearanceRuleSnapshot).
    /// </summary>
    internal sealed class PomiarRegulaSnapshot : IAppearanceRuleProperties
    {
        public string Name { get; init; } = string.Empty;
        public string ZapisaneKryterium { get; init; } = string.Empty;
        public string ZapisaneCele { get; init; } = string.Empty;
        public string ZapisanyKontekst { get; init; } = string.Empty;
        public Type ZapisanyTyp { get; init; }
        public Color? ZapisanyKolorTla { get; init; }
        public Color? ZapisanyKolorCzcionki { get; init; }
        public DXFontStyle? ZapisanyStylCzcionki { get; init; }
        public int Priority { get; init; }

        public string TargetItems { get => ZapisaneCele; set { } }
        public string AppearanceItemType { get => nameof(DevExpress.ExpressApp.ConditionalAppearance.AppearanceItemType.ViewItem); set { } }
        public string Criteria { get => ZapisaneKryterium; set { } }
        public string Method { get => string.Empty; set { } }
        public string Context { get => ZapisanyKontekst; set { } }
        public Type DeclaringType => ZapisanyTyp;

        int IAppearance.Priority { get => Priority; set { } }
        DXFontStyle? IAppearance.FontStyle { get => ZapisanyStylCzcionki; set { } }
        Color? IAppearance.FontColor { get => ZapisanyKolorCzcionki; set { } }
        Color? IAppearance.BackColor { get => ZapisanyKolorTla; set { } }
        ViewItemVisibility? IAppearance.Visibility { get => null; set { } }
        bool? IAppearance.Enabled { get => null; set { } }
    }

    /// <summary>Definicje trzech regul uzywanych w obu osiach pomiaru.</summary>
    public static class PomiarRegulyDefinicje
    {
        public sealed record Definicja(string Nazwa, string Kryterium, Color KolorTla, Color KolorCzcionki);

        public static readonly Definicja[] Bazowe =
        {
            new("NiezaplaconaFaktura", "[Zaplacona] = False", Color.FromArgb(255, 235, 235), Color.FromArgb(153, 0, 0)),
            new("DuzaKwota", "[Kwota] > 10000", Color.FromArgb(235, 245, 255), Color.FromArgb(0, 51, 153)),
            new("PoTerminie", "[TerminPlatnosci] < LocalDateTimeToday()", Color.FromArgb(255, 248, 225), Color.FromArgb(153, 102, 0))
        };

        /// <summary>Lista regul rozmnozona przez POMIAR_MNOZNIK (do badania progu widocznosci).</summary>
        public static Definicja[] Aktywne()
        {
            int m = PomiarKonfiguracja.MnoznikRegul;
            if (m <= 1) return Bazowe;
            var wynik = new List<Definicja>(Bazowe.Length * m);
            for (int i = 0; i < m; i++)
                foreach (var d in Bazowe)
                    wynik.Add(d with { Nazwa = $"{d.Nazwa}_{i}" });
            return wynik.ToArray();
        }
    }
}
