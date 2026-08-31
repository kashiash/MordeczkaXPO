using System.ComponentModel;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace XafXPODynAssem.Module.BusinessObjects
{
    /// <summary>
    /// PROTOTYP DO WYRZUCENIA — encja wylacznie na potrzeby pomiaru czasu renderowania
    /// duzej listy z regulami wygladu (galaz eksperyment/pomiar-regul).
    /// Same pola skalarne, zadnych referencji — zeby Fit() nie odpalal leniwego ladowania.
    /// </summary>
    [DefaultClassOptions]
    [NavigationItem("Pomiar")]
    [DefaultProperty(nameof(Numer))]
    [XafDisplayName("Faktury (pomiar)")]
    public class PomiarFaktura : BaseObject
    {
        public PomiarFaktura(Session session) : base(session) { }

        string numer;
        [XafDisplayName("Numer")]
        [Size(100)]
        public string Numer
        {
            get => numer;
            set => SetPropertyValue(nameof(Numer), ref numer, value);
        }

        decimal kwota;
        [XafDisplayName("Kwota")]
        public decimal Kwota
        {
            get => kwota;
            set => SetPropertyValue(nameof(Kwota), ref kwota, value);
        }

        bool zaplacona;
        [XafDisplayName("Zapłacona")]
        public bool Zaplacona
        {
            get => zaplacona;
            set => SetPropertyValue(nameof(Zaplacona), ref zaplacona, value);
        }

        DateTime terminPlatnosci;
        [XafDisplayName("Termin płatności")]
        public DateTime TerminPlatnosci
        {
            get => terminPlatnosci;
            set => SetPropertyValue(nameof(TerminPlatnosci), ref terminPlatnosci, value);
        }

        string kontrahent;
        [XafDisplayName("Kontrahent")]
        [Size(100)]
        public string Kontrahent
        {
            get => kontrahent;
            set => SetPropertyValue(nameof(Kontrahent), ref kontrahent, value);
        }

        string opis;
        [XafDisplayName("Opis")]
        [Size(200)]
        public string Opis
        {
            get => opis;
            set => SetPropertyValue(nameof(Opis), ref opis, value);
        }
    }
}
