namespace SmartNav.Models
{
    public class AdminBaseRequest
    {
        public int AdminUserId { get; set; }
    }

    public class AdminUsersRequest : AdminBaseRequest
    {
        public string? Search { get; set; }
    }

    public class AdminUpdateUserRoleRequest : AdminBaseRequest
    {
        public int TargetUserId { get; set; }
        public int NewRoleId { get; set; }
    }

    public class AdminUpdateUserVerificationRequest : AdminBaseRequest
    {
        public int TargetUserId { get; set; }
        public bool IsVerified { get; set; }
    }

    public class AdminDeleteUserRequest : AdminBaseRequest
    {
        public int TargetUserId { get; set; }
    }

    public class AdminAuditLogsRequest : AdminBaseRequest
    {
        public int Take { get; set; } = 100;
    }

    public class AdminBulkUserChangesRequest : AdminBaseRequest
    {
        public List<AdminBulkUserChangeItem> Changes { get; set; } = new();
    }

    public class AdminBulkUserChangeItem
    {
        public int TargetUserId { get; set; }
        public int? NewRoleId { get; set; }
        public bool? IsVerified { get; set; }
    }

    public class AdminAnalyticsRequest : AdminBaseRequest
    {
    }

    public class AdminAnalyticsByUserRequest : AdminBaseRequest
    {
        public int TargetUserId { get; set; }
        public int? TripId { get; set; }
    }
}
