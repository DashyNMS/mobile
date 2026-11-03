namespace DashyNMS.Mobile.Services;

/// <summary>Restores page-owned controls after Shell selects a retained stack or resumes.</summary>
public interface IActivePage
{
    void Activate();
}
