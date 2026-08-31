using XafXPODynAssem.Module.Validation;
using Xunit;

namespace XafXPODynAssem.Module.Tests
{
    /// <summary>
    /// Nazwy klas i pol przychodza z czatu, wiec walidacja nazw jest pierwsza linia obrony
    /// przed metadana, ktora nie skompiluje sie w Roslynie albo zderzy sie z XPO.
    /// </summary>
    public class NameValidationTests
    {
        [Theory(DisplayName = "Poprawny identyfikator klasy przechodzi")]
        [InlineData("Faktura")]
        [InlineData("FakturaPozycja")]
        [InlineData("_prywatna")]
        [InlineData("Klasa123")]
        public void ValidClassIdentifierIsAccepted(string name)
        {
            Assert.True(CustomClassValidation.IsValidIdentifier(name));
        }

        [Theory(DisplayName = "Niepoprawny identyfikator klasy jest odrzucany")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("123Klasa")]
        [InlineData("Nazwa Ze Spacja")]
        [InlineData("Nazwa-Z-Myslnikiem")]
        [InlineData("Faktura.Pozycja")]
        public void InvalidClassIdentifierIsRejected(string name)
        {
            Assert.False(CustomClassValidation.IsValidIdentifier(name));
        }

        [Fact(DisplayName = "Polskie znaki w nazwie klasy sa odrzucane")]
        public void PolishCharactersAreRejectedInClassName()
        {
            // Wazne dla narzedzi AI: uzytkownik dyktuje po polsku, wiec model latwo
            // zaproponuje "Zlecenie" z ogonkami. Regex dopuszcza tylko [A-Za-z_][A-Za-z0-9_]*.
            Assert.False(CustomClassValidation.IsValidIdentifier("Zamówienie"));
            Assert.False(CustomClassValidation.IsValidIdentifier("Wysyłka"));
            Assert.False(CustomClassValidation.IsValidIdentifier("Łąka"));
        }

        [Theory(DisplayName = "Slowo kluczowe C# jest rozpoznawane")]
        [InlineData("class")]
        [InlineData("string")]
        [InlineData("int")]
        [InlineData("namespace")]
        public void CSharpKeywordIsDetected(string name)
        {
            Assert.True(CustomClassValidation.IsCSharpKeyword(name));
        }

        [Fact(DisplayName = "Rozpoznawanie slow kluczowych rozroznia wielkosc liter")]
        public void KeywordDetectionIsCaseSensitive()
        {
            // "Class" jest legalna nazwa klasy w C#, "class" nie.
            Assert.True(CustomClassValidation.IsCSharpKeyword("class"));
            Assert.False(CustomClassValidation.IsCSharpKeyword("Class"));
        }

        [Theory(DisplayName = "Nazwa zarezerwowana przez platforme jest rozpoznawana")]
        [InlineData("BaseObject")]
        [InlineData("CustomClass")]
        [InlineData("CustomField")]
        [InlineData("Session")]
        [InlineData("XPObject")]
        public void ReservedTypeNameIsDetected(string name)
        {
            Assert.True(CustomClassValidation.IsReservedTypeName(name));
        }

        [Fact(DisplayName = "Faktura nie jest nazwa zarezerwowana")]
        public void DomainNameIsNotReserved()
        {
            Assert.False(CustomClassValidation.IsReservedTypeName("Faktura"));
        }

        [Theory(DisplayName = "Nazwa pola zarezerwowana przez XPO jest rozpoznawana niezaleznie od wielkosci liter")]
        [InlineData("Oid")]
        [InlineData("oid")]
        [InlineData("OID")]
        [InlineData("GCRecord")]
        [InlineData("gcrecord")]
        [InlineData("ObjectType")]
        [InlineData("OptimisticLockField")]
        public void ReservedFieldNameIsDetectedCaseInsensitively(string name)
        {
            // Rozroznienie wobec nazw klas jest celowe: XPO tworzy te kolumny samo,
            // a baza nie rozroznia wielkosci liter tak jak C#.
            Assert.True(CustomFieldValidation.IsReservedFieldName(name));
        }

        [Fact(DisplayName = "Kwota nie jest nazwa zarezerwowana pola")]
        public void DomainFieldNameIsNotReserved()
        {
            Assert.False(CustomFieldValidation.IsReservedFieldName("Kwota"));
            Assert.False(CustomFieldValidation.IsReservedFieldName("Zaplacona"));
        }
    }
}
