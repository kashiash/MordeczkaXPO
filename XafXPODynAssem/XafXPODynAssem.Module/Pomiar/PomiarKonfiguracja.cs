using System.Threading;

namespace XafXPODynAssem.Module.Pomiar
{
    /// <summary>
    /// PROTOTYP DO WYRZUCENIA (galaz eksperyment/pomiar-regul).
    /// Konfiguracja pomiaru czytana raz przy starcie procesu ze zmiennych srodowiskowych.
    /// </summary>
    public static class PomiarKonfiguracja
    {
        /// <summary>brak | stary | nowy — os CollectAppearanceRules.</summary>
        public static string Tryb { get; } = Odczytaj("POMIAR_TRYB", "brak");

        /// <summary>brak | stary | nowy — os CustomizeElement na DxGridListEditor.</summary>
        public static string TrybCe { get; } = Odczytaj("POMIAR_CE", "brak");

        /// <summary>Rozmiar strony gridu wymuszany na widoku PomiarFaktura_ListView.</summary>
        public static int RozmiarStrony { get; } = OdczytajInt("POMIAR_STRONA", 100);

        /// <summary>Mnoznik liczby regul (1 = trzy reguly bazowe).</summary>
        public static int MnoznikRegul { get; } = OdczytajInt("POMIAR_MNOZNIK", 1);

        /// <summary>Zbieranie rozkladu typow elementow gridu — TYLKO do sondy, alokuje w petli.</summary>
        public static bool ZbierajRozklad { get; } = Environment.GetEnvironmentVariable("POMIAR_ROZKLAD") == "1";

        private static string Odczytaj(string nazwa, string domyslna)
        {
            var v = Environment.GetEnvironmentVariable(nazwa);
            return string.IsNullOrWhiteSpace(v) ? domyslna : v.Trim().ToLowerInvariant();
        }

        private static int OdczytajInt(string nazwa, int domyslna)
        {
            var v = Environment.GetEnvironmentVariable(nazwa);
            return int.TryParse(v, out var i) && i > 0 ? i : domyslna;
        }
    }

    /// <summary>
    /// Liczniki wywolan handlerow — bez nich nie da sie odniesc wyniku end-to-end
    /// do mikro-benchmarku (tam scenariusze liczono w wywolaniach handlera).
    /// </summary>
    public static class PomiarLicznik
    {
        private static long _wywolaniaCe;
        private static long _wywolaniaCeZTrafieniem;
        private static long _zebranieRegul;
        private static long _budowaEvaluatora;
        private static long _zastosowania;

        public static long WywolaniaCe => Interlocked.Read(ref _wywolaniaCe);
        public static long WywolaniaCeZTrafieniem => Interlocked.Read(ref _wywolaniaCeZTrafieniem);
        public static long ZebranieRegul => Interlocked.Read(ref _zebranieRegul);
        public static long BudowaEvaluatora => Interlocked.Read(ref _budowaEvaluatora);
        public static long Zastosowania => Interlocked.Read(ref _zastosowania);

        public static void DodajCe() => Interlocked.Increment(ref _wywolaniaCe);
        public static void DodajCeTrafienie() => Interlocked.Increment(ref _wywolaniaCeZTrafieniem);
        public static void DodajZebranie() => Interlocked.Increment(ref _zebranieRegul);
        public static void DodajBudowe() => Interlocked.Increment(ref _budowaEvaluatora);
        public static void DodajZastosowanie() => Interlocked.Increment(ref _zastosowania);

        public static void Zeruj()
        {
            Interlocked.Exchange(ref _wywolaniaCe, 0);
            Interlocked.Exchange(ref _wywolaniaCeZTrafieniem, 0);
            Interlocked.Exchange(ref _zebranieRegul, 0);
            Interlocked.Exchange(ref _budowaEvaluatora, 0);
            Interlocked.Exchange(ref _zastosowania, 0);
        }
    }
}
