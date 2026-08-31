using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ConditionalAppearance;
using XafXPODynAssem.Module.BusinessObjects;

namespace XafXPODynAssem.Module.Pomiar
{
    /// <summary>
    /// PROTOTYP DO WYRZUCENIA (galaz eksperyment/pomiar-regul).
    /// Os pierwsza pomiaru: reguly wygladu dokladane przez
    /// <c>AppearanceController.CollectAppearanceRules</c>.
    ///
    /// UWAGA METODOLOGICZNA: to zdarzenie sluzy do ZBIERANIA regul i odpala sie
    /// raz na aktywacje widoku, a nie raz na wiersz. Wariant "stary" i "nowy"
    /// sa tu wiec IDENTYCZNE — ewaluacja kryterium nalezy do silnika DevExpressa,
    /// ktorego nie kontrolujemy. Roznica stary/nowy jest mierzalna dopiero na osi
    /// CustomizeElement (PomiarCustomizeElementController).
    /// </summary>
    public class PomiarCollectRulesController : ObjectViewController<ListView, PomiarFaktura>
    {
        private PomiarRegulaSnapshot[] reguly;

        protected override void OnActivated()
        {
            base.OnActivated();
            if (PomiarKonfiguracja.Tryb == "brak") return;

            var appearanceController = Frame.GetController<AppearanceController>();
            if (appearanceController == null) return;

            reguly = null;
            appearanceController.ResetRulesCache();
            appearanceController.CollectAppearanceRules += NaZbieranieRegul;
            appearanceController.AppearanceApplied += NaZastosowanie;
            appearanceController.Refresh();
        }

        protected override void OnDeactivated()
        {
            var appearanceController = Frame.GetController<AppearanceController>();
            if (appearanceController != null)
            {
                appearanceController.CollectAppearanceRules -= NaZbieranieRegul;
                appearanceController.AppearanceApplied -= NaZastosowanie;
            }
            reguly = null;
            base.OnDeactivated();
        }

        private void NaZastosowanie(object sender, ApplyAppearanceEventArgs e)
        {
            PomiarLicznik.DodajZastosowanie();
        }

        private void NaZbieranieRegul(object sender, CollectAppearanceRulesEventArgs e)
        {
            if (ObjectSpace is NonPersistentObjectSpace) return;
            if (ObjectSpace == null || ObjectSpace.IsDisposed) return;

            PomiarLicznik.DodajZebranie();

            if (reguly == null)
            {
                var typ = typeof(PomiarFaktura);
                int priorytet = 100;
                reguly = PomiarRegulyDefinicje.Aktywne().Select(d => new PomiarRegulaSnapshot
                {
                    Name = d.Nazwa,
                    ZapisaneKryterium = d.Kryterium,
                    ZapisaneCele = "*",            // "*" = wszystkie kolumny (caly wiersz)
                    ZapisanyKontekst = "ListView",
                    ZapisanyTyp = typ,
                    ZapisanyKolorTla = d.KolorTla,
                    ZapisanyKolorCzcionki = d.KolorCzcionki,
                    Priority = priorytet++
                }).ToArray();
            }

            e.AppearanceRules.AddRange(reguly);
        }
    }
}
