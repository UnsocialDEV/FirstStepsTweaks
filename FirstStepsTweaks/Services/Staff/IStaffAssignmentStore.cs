namespace FirstStepsTweaks.Services
{
    public interface IStaffAssignmentStore
    {
        StaffRoster LoadRoster();

        void SaveRoster(StaffRoster roster);
    }
}
