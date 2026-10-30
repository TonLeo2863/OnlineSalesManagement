using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
namespace OnlineSalesManagement.Services
{
    public static class PeopleDirectory
    {
        public static readonly string[] Positions = { "Quản lý cửa hàng", "Quản lý kho", "Nhân viên kho", "Nhân viên bán hàng", "Kế toán", "Nhân viên giao hàng", "Khác" };
        public static void CheckDates(ModelStateDictionary state, DateTime? from, DateTime? to)
        {
            var first = new DateTime(1753, 1, 1); var last = new DateTime(9999, 12, 30);
            if (from.HasValue && (from.Value.Date < first || from.Value.Date > last) || to.HasValue && (to.Value.Date < first || to.Value.Date > last)) state.AddModelError("", "Ngày lọc ngoài khoảng hỗ trợ 1753–9999.");
            if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date) state.AddModelError("", "Từ ngày phải trước hoặc bằng đến ngày.");
        }
        public static string ContactSearch(string phone, ModelStateDictionary state)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            if (!Regex.IsMatch(phone.Trim(), @"^\+?[0-9\s().-]{3,25}$")) { state.AddModelError("phone", "Số điện thoại tìm kiếm không hợp lệ."); return null; }
            phone = Regex.Replace(phone, @"[\s().-]", "");
            return phone.StartsWith("+84") ? "0" + phone.Substring(3) : phone;
        }
        public static string ResolvePosition(string choice, string raw)
        {
            if (string.IsNullOrEmpty(choice)) return raw; // Compatibility with existing forms/operators; legacy values are not rewritten.
            if (Positions.Contains(choice)) return choice;
            if (choice != "__other__") throw new ArgumentException("Chức vụ được chọn không hợp lệ.");
            raw = (raw ?? "").Trim();
            if (raw.Length < 1 || raw.Length > 100 || raw.Any(char.IsControl)) throw new ArgumentException("Chức vụ khác cần từ 1 đến 100 ký tự hợp lệ.");
            return raw;
        }
    }
}
