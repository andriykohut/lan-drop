using System.Globalization;

namespace LanDrop;

/// <summary>On-screen text follows the Windows display language: Ukrainian for Ukrainian, English otherwise.</summary>
static class Lang
{
    static readonly bool Ukrainian = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "uk";

    public static string T(string uk, string en) => Ukrainian ? uk : en;
}
