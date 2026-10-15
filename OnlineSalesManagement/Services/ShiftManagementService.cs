
using System;
using System.Data;
using System.Linq;
using OnlineSalesManagement;
using OnlineSalesManagement.Models;

namespace OnlineSalesManagement.Services
{
    public class ShiftManagementService
    {
        // Admin tạo mẫu ca: sáng, chiều, đêm...
        public int CreateTemplate(
            string name,
            TimeSpan startTime,
            TimeSpan endTime)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                name.Trim().Length > 100)
                throw new ArgumentException("Tên ca không hợp lệ.");

            ValidateTime(startTime);
            ValidateTime(endTime);

            if (startTime == endTime)
                throw new ArgumentException(
                    "Giờ bắt đầu và kết thúc không được trùng nhau.");

            using (var db = new ApplicationDbContext())
            {
                var template = new ShiftTemplate
                {
                    Name = name.Trim(),
                    StartTime = startTime,
                    EndTime = endTime,
                    EndsNextDay = endTime < startTime,
                    IsActive = true,
                    IsSystemDefault = false
                };

                db.ShiftTemplates.Add(template);
                db.SaveChanges();
                return template.Id;
            }
        }

        // Lên lịch một ca từ mẫu có sẵn.
        public int ScheduleFromTemplate(
            int templateId,
            DateTime workDate,
            string assignedUserId,
            string adminUserId)
        {
            ValidateActor(adminUserId);

            using (var db = new ApplicationDbContext())
            {
                var template = db.ShiftTemplates
                    .SingleOrDefault(t => t.Id == templateId);

                if (template == null || !template.IsActive)
                    throw new InvalidOperationException(
                        "Mẫu ca không tồn tại hoặc đã ngừng sử dụng.");

                var start = workDate.Date.Add(template.StartTime);
                var end = workDate.Date.Add(template.EndTime);

                if (template.EndsNextDay)
                    end = end.AddDays(1);

                return SaveScheduledShift(
                    db, template.Name, templateId,
                    start, end, assignedUserId, adminUserId);
            }
        }

        // Admin tự cấu hình thời gian bắt đầu/kết thúc.
        public int ScheduleCustom(
            string name,
            DateTime start,
            DateTime end,
            string assignedUserId,
            string adminUserId)
        {
            ValidateActor(adminUserId);

            using (var db = new ApplicationDbContext())
            {
                return SaveScheduledShift(
                    db, name, null,
                    start, end, assignedUserId, adminUserId);
            }
        }

        private int SaveScheduledShift(
            ApplicationDbContext db,
            string name,
            int? templateId,
            DateTime start,
            DateTime end,
            string assignedUserId,
            string adminUserId)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                name.Trim().Length > 100)
                throw new ArgumentException("Tên ca không hợp lệ.");

            if (start >= end || end - start > TimeSpan.FromHours(24))
                throw new ArgumentException(
                    "Ca phải có thời lượng lớn hơn 0 và không quá 24 giờ.");

            if (string.IsNullOrWhiteSpace(assignedUserId) ||
                assignedUserId.Length > 128)
                throw new ArgumentException(
                    "Vui lòng chọn tài khoản phụ trách ca.");

            using (var tx = db.Database.BeginTransaction(
                IsolationLevel.Serializable))
            {
                if (!db.Users.Any(u => u.Id == assignedUserId))
                    throw new InvalidOperationException(
                        "Tài khoản phụ trách không tồn tại.");

                // Không xếp hai ca chồng giờ cho cùng một người.
                bool overlapping = db.WorkShifts.Any(s =>
                    s.AssignedUserId == assignedUserId &&
                    s.Status != WorkShiftStatus.Cancelled &&
                    s.ScheduledStart < end &&
                    s.ScheduledEnd > start);

                if (overlapping)
                    throw new InvalidOperationException(
                        "Nhân viên đã được xếp ca trong khoảng thời gian này.");

                var shift = new WorkShift
                {
                    Name = name.Trim(),
                    ShiftTemplateId = templateId,
                    ScheduledStart = start,
                    ScheduledEnd = end,
                    AssignedUserId = assignedUserId,
                    Status = WorkShiftStatus.Scheduled
                };

                db.WorkShifts.Add(shift);
                db.SaveChanges();

                db.InventoryShiftAuditLogs.Add(
                    new InventoryShiftAuditLog
                    {
                        WorkShiftId = shift.Id,
                        Action = InventoryShiftAuditAction.Created,
                        PerformedByUserId = adminUserId,
                        Reason = "Tạo lịch ca trực"
                    });

                db.SaveChanges();
                tx.Commit();

                return shift.Id;
            }
        }

        // Nhân viên được phân công hoặc Admin mở ca.
        // isAdmin phải do Controller xác định từ quyền thật.
        public void OpenShift(
            int shiftId,
            string userId,
            bool isAdmin)
        {
            ValidateActor(userId);

            using (var db = new ApplicationDbContext())
            using (var tx = db.Database.BeginTransaction(
                IsolationLevel.Serializable))
            {
                if (!db.Users.Any(u => u.Id == userId))
                    throw new InvalidOperationException(
                        "Tài khoản không tồn tại.");

                var shift = db.WorkShifts.SqlQuery(
                    "SELECT * FROM WorkShifts WITH (UPDLOCK, HOLDLOCK) " +
                    "WHERE Id = @p0", shiftId)
                    .SingleOrDefault();

                if (shift == null)
                    throw new InvalidOperationException(
                        "Không tìm thấy ca trực.");

                if (shift.Status != WorkShiftStatus.Scheduled)
                    throw new InvalidOperationException(
                        "Chỉ được mở ca đang ở trạng thái Scheduled.");

                if (!isAdmin && shift.AssignedUserId != userId)
                    throw new UnauthorizedAccessException(
                        "Bạn không được phân công ca này.");

                shift.Status = WorkShiftStatus.Open;
                shift.ActualStart = DateTime.Now;
                shift.OpenedByUserId = userId;

                db.InventoryShiftAuditLogs.Add(
                    new InventoryShiftAuditLog
                    {
                        WorkShiftId = shift.Id,
                        Action = InventoryShiftAuditAction.Opened,
                        PerformedByUserId = userId,
                        Reason = "Mở ca trực"
                    });

                db.SaveChanges();
                tx.Commit();
            }
        }

        private static void ValidateTime(TimeSpan time)
        {
            if (time < TimeSpan.Zero ||
                time >= TimeSpan.FromDays(1))
                throw new ArgumentException(
                    "Giờ phải nằm trong khoảng 00:00 đến 23:59.");
        }

        private static void ValidateActor(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) ||
                userId.Length > 128)
                throw new ArgumentException(
                    "Thông tin tài khoản thực hiện không hợp lệ.");
        }
    }
}
