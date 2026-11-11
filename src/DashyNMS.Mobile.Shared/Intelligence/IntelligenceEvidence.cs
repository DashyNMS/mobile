using DesktopNMS.Core.Intelligence;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// One object a lookup showed the model, as an answer's chips need it (#228).
/// Core's <see cref="IntelligenceInlineReference.Name"/> is display text: lookups
/// clean and shorten network labels so one can't break a row or forge a marker.
/// Devices filters groups and locations by LibreNMS's exact name, so a cleaned
/// name would open an empty list; <see cref="Filter"/> keeps the name exactly as
/// LibreNMS returned it, apart from the display text. It is plain data, so saved
/// chats (#229) can store it with the reference it belongs to.
/// </summary>
/// <param name="Reference">What the chip shows, and the typed object it stands for.</param>
/// <param name="Filter">The unaltered group or location name that Devices filters by; null for other kinds.</param>
public sealed record IntelligenceEvidence(IntelligenceInlineReference Reference, string? Filter = null);
