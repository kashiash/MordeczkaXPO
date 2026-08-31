using System.Collections.Concurrent;
using System.Drawing;
using DevExpress.Blazor;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Blazor.Editors;
using XafXPODynAssem.Module.BusinessObjects;
using XafXPODynAssem.Module.Pomiar;

namespace XafXPODynAssem.Blazor.Server.Controllers
{
    /// <summary>
    /// PROTOTYP DO WYRZUCENIA (galaz eksperyment/pomiar-regul).
    /// Os druga pomiaru: reguly wygladu nakladane recznie w
    /// <c>DxGridModel.CustomizeElement</c> — tu roznica "stary vs nowy" jest realna,
    /// bo handler odpala sie per element gridu i to MY budujemy evaluator.
    ///
    /// POMIAR_CE = brak | stary | nowy
    /// POMIAR_STRONA = rozmiar strony gridu (wymuszany na widoku PomiarFaktura_ListView)
    /// </summary>
    public class PomiarCustomizeElementController : ObjectViewController<ListView, PomiarFaktura>
    {
        private sealed record Regula(string Kryterium, string KolorTla, string KolorCzcionki);

        private Regula[] reguly;
        // Wariant NOWY: evaluator zbudowany RAZ na regule, przy aktywacji widoku.
        private (DevExpress.Data.Filtering.Helpers.ExpressionEvaluator Ewaluator, string KolorTla, string KolorCzcionki)[] evaluatory;

        /// <summary>Rozklad wywolan handlera na typy elementow gridu — bez tego wynik jest nieinterpretowalny.</summary>
        public static readonly ConcurrentDictionary<string, long> RozkladElementow = new();

        protected override void OnActivated()
        {
            base.OnActivated();
            if (PomiarKonfiguracja.TrybCe == "brak") return;

            reguly = PomiarRegulyDefinicje.Aktywne()
                .Select(d => new Regula(d.Kryterium, NaHex(d.KolorTla), NaHex(d.KolorCzcionki)))
                .ToArray();

            if (PomiarKonfiguracja.TrybCe == "nowy")
            {
                if (ObjectSpace is NonPersistentObjectSpace || ObjectSpace == null || ObjectSpace.IsDisposed) return;
                var deskryptor = ObjectSpace.GetEvaluatorContextDescriptor(typeof(PomiarFaktura));
                evaluatory = reguly
                    .Select(r =>
                    {
                        PomiarLicznik.DodajBudowe();
                        return (ObjectSpace.GetExpressionEvaluator(deskryptor, CriteriaOperator.Parse(r.Kryterium)), r.KolorTla, r.KolorCzcionki);
                    })
                    .ToArray();
            }
        }

        protected override void OnDeactivated()
        {
            reguly = null;
            evaluatory = null;
            base.OnDeactivated();
        }

        protected override void OnViewControlsCreated()
        {
            base.OnViewControlsCreated();
            if (View.Editor is not DxGridListEditor editor) return;

            var model = editor.GetGridAdapter().GridModel;

            // Deterministyczny rozmiar strony — inaczej pomiar mierzy ustawienie z modelu XAF.
            model.PageSize = PomiarKonfiguracja.RozmiarStrony;
            model.PageSizeSelectorVisible = true;
            model.PageSizeSelectorItems = new[] { 100, 500, 1000 };
            model.VirtualScrollingEnabled = false;

            Console.WriteLine($"[POMIAR] widok={View.Id} strona={PomiarKonfiguracja.RozmiarStrony} " +
                              $"tryb={PomiarKonfiguracja.Tryb} ce={PomiarKonfiguracja.TrybCe} " +
                              $"mnoznik={PomiarKonfiguracja.MnoznikRegul} " +
                              $"dostepDanych={View.CollectionSource?.DataAccessMode}");

            if (PomiarKonfiguracja.TrybCe == "brak") return;

            model.CustomizeElement += NaElement;
        }

        private void NaElement(GridCustomizeElementEventArgs e)
        {
            // Rozklad typow elementow alokuje string na KAZDE wywolanie (8008 razy przy
            // stronie 1000), wiec w serii pomiarowej jest wylaczony — inaczej zanieczyszcza
            // pomiar alokacji. Wlaczamy go tylko w sondzie: POMIAR_ROZKLAD=1.
            if (PomiarKonfiguracja.ZbierajRozklad)
                RozkladElementow.AddOrUpdate(e.ElementType.ToString(), 1, (_, v) => v + 1);
            if (e.ElementType != GridElementType.DataRow) return;

            PomiarLicznik.DodajCe();
            object obiekt = e.Grid.GetDataItem(e.VisibleIndex);
            if (obiekt == null) return;

            if (PomiarKonfiguracja.TrybCe == "nowy")
            {
                if (evaluatory == null) return;
                foreach (var (ewaluator, tlo, czcionka) in evaluatory)
                {
                    if (!ewaluator.Fit(obiekt)) continue;
                    PomiarLicznik.DodajCeTrafienie();
                    Pomaluj(e, tlo, czcionka);
                }
                return;
            }

            // WARIANT STARY — wzorzec z DataDrive IObjectSpaceAppearanceExtensions.FitAppearance:
            // deskryptor + CriteriaOperator.Parse + evaluator budowane OD NOWA dla kazdego wiersza i kazdej reguly.
            if (reguly == null || ObjectSpace == null || ObjectSpace.IsDisposed) return;
            foreach (var r in reguly)
            {
                var deskryptor = ObjectSpace.GetEvaluatorContextDescriptor(typeof(PomiarFaktura));
                var ewaluator = ObjectSpace.GetExpressionEvaluator(deskryptor, CriteriaOperator.Parse(r.Kryterium));
                PomiarLicznik.DodajBudowe();
                if (!ewaluator.Fit(obiekt)) continue;
                PomiarLicznik.DodajCeTrafienie();
                Pomaluj(e, r.KolorTla, r.KolorCzcionki);
            }
        }

        private static void Pomaluj(GridCustomizeElementEventArgs e, string tlo, string czcionka)
        {
            string styl = $"background-color:{tlo} !important;color:{czcionka} !important;";
            e.Style = string.IsNullOrWhiteSpace(e.Style) ? styl : e.Style + styl;
        }

        private static string NaHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}
