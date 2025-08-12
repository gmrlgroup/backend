using Application.Shared.Models;

namespace Application.Shared.Services;

public class StateContainer
{
    private string? savedString;

    private Company company;


    public Company Company
    {
        get => company;
        set
        {
            company = value;
            NotifyStateChanged();
        }
    }


    public string Property
    {
        get => savedString ?? string.Empty;
        set
        {
            savedString = value;
            NotifyStateChanged();
        }
    }

    public event Action? OnChange;

    private void NotifyStateChanged() => OnChange?.Invoke();
}
