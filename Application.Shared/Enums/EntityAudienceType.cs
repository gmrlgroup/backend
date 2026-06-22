namespace Application.Shared.Enums;

/// <summary>
/// The role a user plays for a monitored entity. Drives who is notified on incidents.
/// </summary>
public enum EntityAudienceType
{
    Owner = 1,
    Maintainer = 2,
    Stakeholder = 3
}
