using System;
using System.ComponentModel.DataAnnotations;
namespace OnlineSalesManagement.Models
{
    // No email, telephone, credentials or tokens in the audit payload.
    public sealed class AccountAdministrationAudit
    {
        public int Id { get; set; }
        [Required, StringLength(128)] public string TargetUserId { get; set; }
        [Required, StringLength(128)] public string ActorUserId { get; set; }
        [Required, StringLength(20)] public string Action { get; set; }
        [Required, StringLength(500)] public string Reason { get; set; }
        public DateTime OccurredUtc { get; set; }
    }
}
