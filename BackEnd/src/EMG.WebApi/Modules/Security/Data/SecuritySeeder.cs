using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Security.Data;

public static class SecuritySeeder
{
    public static async Task SeedSecurityAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetService<EmgDbContext>();
        if (db is null || !await db.Database.CanConnectAsync()) return;

        var permissions = new[]
        {
            new Permission { Code = "projects.view", DisplayName = "عرض المشاريع" },
            new Permission { Code = "projects.create", DisplayName = "إضافة مشروع" },
            new Permission { Code = "projects.update", DisplayName = "تعديل مشروع" },
            new Permission { Code = "users.manage", DisplayName = "إدارة المستخدمين" },
            new Permission { Code = "roles.manage", DisplayName = "إدارة الأدوار والصلاحيات" },
            new Permission { Code = "data.migrate", DisplayName = "ترحيل البيانات" },
            new Permission { Code = "project-imports.approve", DisplayName = "اعتماد ترحيل المشروع" },
            new Permission { Code = "project-imports.reject", DisplayName = "رفض ترحيل المشروع" },
            new Permission { Code = "project-imports.import-approved-history", DisplayName = "استيراد ملفات تاريخية معتمدة" },
            new Permission { Code = "suppliers.manage", DisplayName = "إدارة الموردين" },
            new Permission { Code = "site-movements.manage", DisplayName = "إدارة حركة الموقع" },
            new Permission { Code = "advances.manage", DisplayName = "إدارة الدفعات تحت الحساب" },
            new Permission { Code = "work-items.manage", DisplayName = "إدارة بنود الأعمال" },
            new Permission { Code = "final-accounts.view", DisplayName = "عرض كشف الحساب النهائي" },
            new Permission { Code = "reports.view", DisplayName = "عرض التقارير" },
            new Permission { Code = "attachments.manage", DisplayName = "إدارة المرفقات" },
            new Permission { Code = "attendance.manage", DisplayName = "إدارة الحضور والانصراف" },
            new Permission { Code = "notifications.view", DisplayName = "عرض الإشعارات" },
            new Permission { Code = "workflow.execute", DisplayName = "تنفيذ إجراءات الاعتماد" },
            new Permission { Code = "transactions.correct", DisplayName = "تصحيح المعاملات المسجلة" },
            new Permission { Code = "transactions.cancel", DisplayName = "إلغاء أو عكس المعاملات" },
            new Permission { Code = "transactions.history", DisplayName = "عرض تاريخ تعديلات المعاملات" },
            new Permission { Code = "search.view", DisplayName = "استخدام البحث" },

            // الصلاحيات التفصيلية الجديدة: كل إجراء حساس له صلاحية مستقلة.
            // البنود العامة أعلاه تظل موجودة للتوافق مع الأدوار القديمة.
            new Permission { Code = "projects.archive", DisplayName = "أرشفة المشروع" },
            new Permission { Code = "projects.export", DisplayName = "تصدير بيانات المشاريع" },
            new Permission { Code = "users.view", DisplayName = "عرض المستخدمين" },
            new Permission { Code = "users.create", DisplayName = "إضافة مستخدم" },
            new Permission { Code = "users.update", DisplayName = "تعديل المستخدم" },
            new Permission { Code = "users.deactivate", DisplayName = "تعطيل أو تفعيل المستخدم" },
            new Permission { Code = "users.assign-project", DisplayName = "إسناد المشاريع للمستخدمين" },
            new Permission { Code = "users.reset-password", DisplayName = "إعادة تعيين كلمة المرور" },
            new Permission { Code = "roles.view", DisplayName = "عرض الأدوار والصلاحيات" },
            new Permission { Code = "roles.update", DisplayName = "تعديل صلاحيات الدور" },
            new Permission { Code = "site-movements.view", DisplayName = "عرض حركة الموقع" },
            new Permission { Code = "site-movements.create", DisplayName = "إضافة حركة موقع" },
            new Permission { Code = "site-movements.update", DisplayName = "تعديل حركة موقع" },
            new Permission { Code = "site-movements.submit", DisplayName = "إرسال حركة الموقع للمراجعة" },
            new Permission { Code = "site-movements.approve", DisplayName = "اعتماد حركة الموقع" },
            new Permission { Code = "site-movements.reject", DisplayName = "رفض حركة الموقع" },
            new Permission { Code = "site-movements.post", DisplayName = "ترحيل حركة الموقع" },
            new Permission { Code = "site-movements.export", DisplayName = "تصدير حركة الموقع" },
            new Permission { Code = "suppliers.view", DisplayName = "عرض الموردين وحساباتهم" },
            new Permission { Code = "suppliers.create", DisplayName = "إضافة مورد" },
            new Permission { Code = "suppliers.update", DisplayName = "تعديل بيانات المورد" },
            new Permission { Code = "suppliers.supplies-create", DisplayName = "تسجيل توريد مورد" },
            new Permission { Code = "suppliers.payments-create", DisplayName = "تسجيل دفعة لمورد" },
            new Permission { Code = "suppliers.submit", DisplayName = "إرسال حركة المورد للمراجعة" },
            new Permission { Code = "suppliers.approve", DisplayName = "اعتماد حركة المورد" },
            new Permission { Code = "suppliers.supplies-accept-technical", DisplayName = "قبول توريدة المورد فنياً" },
            new Permission { Code = "suppliers.supplies-approve-financial", DisplayName = "اعتماد توريدة المورد مالياً" },
            new Permission { Code = "suppliers.reject", DisplayName = "رفض حركة المورد" },
            new Permission { Code = "suppliers.post", DisplayName = "ترحيل حركة المورد" },
            new Permission { Code = "suppliers.export", DisplayName = "تصدير حسابات الموردين" },
            new Permission { Code = "advances.view", DisplayName = "عرض الدفعات تحت الحساب" },
            new Permission { Code = "advances.create", DisplayName = "تسجيل دفعة تحت الحساب" },
            new Permission { Code = "advances.allocate", DisplayName = "توزيع الدفعة تحت الحساب" },
            new Permission { Code = "advances.submit", DisplayName = "إرسال الدفعة تحت الحساب للمراجعة" },
            new Permission { Code = "advances.approve", DisplayName = "اعتماد الدفعة تحت الحساب" },
            new Permission { Code = "advances.reject", DisplayName = "رفض الدفعة تحت الحساب" },
            new Permission { Code = "advances.post", DisplayName = "ترحيل الدفعة تحت الحساب" },
            new Permission { Code = "advances.export", DisplayName = "تصدير كشف الدفعات تحت الحساب" },
            new Permission { Code = "work-items.view", DisplayName = "عرض بنود الأعمال" },
            new Permission { Code = "work-items.create", DisplayName = "إضافة بند أعمال" },
            new Permission { Code = "work-items.update", DisplayName = "تعديل بند أعمال" },
            new Permission { Code = "work-items.approve", DisplayName = "اعتماد بند أعمال" },
            new Permission { Code = "excavation.view", DisplayName = "عرض أعمال الحفر" },
            new Permission { Code = "excavation.create", DisplayName = "تسجيل أعمال حفر" },
            new Permission { Code = "excavation.update", DisplayName = "تعديل أعمال حفر" },
            new Permission { Code = "excavation.submit", DisplayName = "إرسال أعمال الحفر للمراجعة" },
            new Permission { Code = "excavation.approve", DisplayName = "اعتماد أعمال الحفر" },
            new Permission { Code = "excavation.reject", DisplayName = "رفض أعمال الحفر" },
            new Permission { Code = "excavation.post", DisplayName = "ترحيل أعمال الحفر" },
            new Permission { Code = "concrete.view", DisplayName = "عرض أعمال الخرسانات" },
            new Permission { Code = "concrete.create", DisplayName = "تسجيل أعمال خرسانات" },
            new Permission { Code = "concrete.update", DisplayName = "تعديل أعمال الخرسانات" },
            new Permission { Code = "concrete.submit", DisplayName = "إرسال أعمال الخرسانات للمراجعة" },
            new Permission { Code = "concrete.approve", DisplayName = "اعتماد أعمال الخرسانات" },
            new Permission { Code = "concrete.reject", DisplayName = "رفض أعمال الخرسانات" },
            new Permission { Code = "concrete.post", DisplayName = "ترحيل أعمال الخرسانات" },
            new Permission { Code = "steel.view", DisplayName = "عرض أعمال الحديد" },
            new Permission { Code = "steel.create", DisplayName = "تسجيل توريد الحديد" },
            new Permission { Code = "steel.update", DisplayName = "تعديل حركة الحديد" },
            new Permission { Code = "steel.submit", DisplayName = "إرسال حركة الحديد للمراجعة" },
            new Permission { Code = "steel.approve", DisplayName = "اعتماد حركة الحديد" },
            new Permission { Code = "steel.reject", DisplayName = "رفض حركة الحديد" },
            new Permission { Code = "steel.post", DisplayName = "ترحيل حركة الحديد" },
            new Permission { Code = "finishing.view", DisplayName = "عرض أعمال التشطيبات" },
            new Permission { Code = "finishing.create", DisplayName = "تسجيل أعمال التشطيبات" },
            new Permission { Code = "finishing.update", DisplayName = "تعديل أعمال التشطيبات" },
            new Permission { Code = "finishing.submit", DisplayName = "إرسال أعمال التشطيبات للمراجعة" },
            new Permission { Code = "finishing.approve", DisplayName = "اعتماد أعمال التشطيبات" },
            new Permission { Code = "finishing.reject", DisplayName = "رفض أعمال التشطيبات" },
            new Permission { Code = "finishing.post", DisplayName = "ترحيل أعمال التشطيبات" },
            new Permission { Code = "final-accounts.approve", DisplayName = "اعتماد كشف الحساب النهائي" },
            new Permission { Code = "final-accounts.export", DisplayName = "تصدير كشف الحساب النهائي" },
            new Permission { Code = "reports.export", DisplayName = "تصدير التقارير" },
            new Permission { Code = "attachments.view", DisplayName = "عرض المرفقات" },
            new Permission { Code = "attachments.upload", DisplayName = "رفع المرفقات" },
            new Permission { Code = "attachments.delete", DisplayName = "حذف المرفقات" },
            new Permission { Code = "attendance.view", DisplayName = "عرض سجل الحضور والانصراف" },
            new Permission { Code = "attendance.check-in", DisplayName = "تسجيل الحضور" },
            new Permission { Code = "attendance.check-out", DisplayName = "تسجيل الانصراف" }
        };

        foreach (var permission in permissions)
        {
            if (!await db.Permissions.AnyAsync(item => item.Code == permission.Code)) db.Permissions.Add(permission);
        }

        var roleDefinitions = new Dictionary<string, string>
        {
            ["admin"] = "مسؤول النظام",
            ["director"] = "مدير عام",
            ["project_manager"] = "مدير المشروعات",
            ["accountant"] = "المحاسب",
            ["engineer"] = "مهندس موقع",
            ["user"] = "مستخدم"
        };
        var roleMap = new Dictionary<string, Role>();
        var newlyCreatedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, displayName) in roleDefinitions)
        {
            var role = await db.Roles.FirstOrDefaultAsync(item => item.Name == name);
            if (role is null)
            {
                role = new Role { Name = name, DisplayName = displayName };
                db.Roles.Add(role);
                newlyCreatedRoles.Add(name);
            }
            else if (string.IsNullOrWhiteSpace(role.DisplayName) || role.DisplayName == role.Name)
            {
                role.DisplayName = displayName;
            }
            roleMap[name] = role;
        }
        var adminRole = roleMap["admin"];
        var admin = await SecurityBootstrapper.BootstrapAdministratorAsync(
            db,
            app.Configuration,
            app.Environment,
            adminRole);

        await db.SaveChangesAsync();

        var permissionIds = await db.Permissions.Select(permission => permission.Id).ToListAsync();
        foreach (var permissionId in permissionIds)
        {
            if (!await db.RolePermissions.AnyAsync(item => item.RoleId == adminRole.Id && item.PermissionId == permissionId))
                db.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = permissionId });
        }

        await EnsureOperationalRolePermissionsAsync(db, roleMap, newlyCreatedRoles);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureOperationalRolePermissionsAsync(
        EmgDbContext db,
        IReadOnlyDictionary<string, Role> roles,
        IReadOnlySet<string> newlyCreatedRoles)
    {
        var defaults = new Dictionary<string, string[]>
        {
            ["engineer"] =
            [
                "projects.view", "suppliers.view", "suppliers.supplies-create", "suppliers.payments-create", "suppliers.submit", "site-movements.view", "site-movements.create", "site-movements.update", "site-movements.submit",
                "excavation.view", "excavation.create", "excavation.update", "excavation.submit", "concrete.view", "concrete.create", "concrete.update", "concrete.submit",
                "steel.view", "steel.create", "steel.update", "steel.submit", "finishing.view", "finishing.create", "finishing.update", "finishing.submit",
                "attachments.view", "attachments.upload", "notifications.view", "attendance.view", "attendance.check-in", "attendance.check-out", "search.view"
            ],
            ["project_manager"] =
            [
                "projects.view", "projects.create", "projects.update", "projects.export", "project-imports.approve", "project-imports.reject", "reports.view", "reports.export",
                "suppliers.view", "suppliers.submit", "suppliers.approve", "suppliers.reject", "suppliers.post", "suppliers.supplies-accept-technical",
                "site-movements.view", "site-movements.submit", "site-movements.approve", "site-movements.reject", "site-movements.post",
                "advances.view", "advances.submit", "advances.approve", "advances.post",
                "excavation.view", "excavation.submit", "excavation.approve", "excavation.reject", "excavation.post",
                "concrete.view", "concrete.submit", "concrete.approve", "concrete.reject", "concrete.post",
                "steel.view", "steel.submit", "steel.approve", "steel.reject", "steel.post",
                "finishing.view", "finishing.submit", "finishing.approve", "finishing.reject", "finishing.post",
                "final-accounts.view", "final-accounts.approve",
                "attachments.view", "workflow.execute", "notifications.view", "attendance.view", "attendance.check-in", "attendance.check-out", "search.view"
            ],
            ["director"] =
            [
                "projects.view", "projects.create", "projects.update", "projects.export", "project-imports.approve", "project-imports.reject", "reports.view", "reports.export",
                "suppliers.view", "suppliers.submit", "suppliers.approve", "suppliers.reject", "suppliers.post", "suppliers.export",
                "suppliers.supplies-accept-technical", "suppliers.supplies-approve-financial",
                "site-movements.view", "site-movements.submit", "site-movements.approve", "site-movements.reject", "site-movements.post", "site-movements.export",
                "advances.view", "advances.submit", "advances.approve", "advances.reject", "advances.post", "advances.export",
                "excavation.view", "excavation.submit", "excavation.approve", "excavation.reject", "excavation.post",
                "concrete.view", "concrete.submit", "concrete.approve", "concrete.reject", "concrete.post",
                "steel.view", "steel.submit", "steel.approve", "steel.reject", "steel.post",
                "finishing.view", "finishing.submit", "finishing.approve", "finishing.reject", "finishing.post",
                "final-accounts.view", "final-accounts.approve", "final-accounts.export",
                "attachments.view", "workflow.execute", "transactions.correct", "transactions.cancel", "transactions.history", "notifications.view", "attendance.view", "attendance.check-in", "attendance.check-out", "search.view"
            ],
            ["accountant"] =
            [
                "project-imports.import-approved-history",
                "projects.view", "reports.view", "reports.export", "suppliers.view", "suppliers.supplies-create", "suppliers.payments-create", "suppliers.submit", "suppliers.approve", "suppliers.reject", "suppliers.post", "suppliers.export",
                "suppliers.supplies-approve-financial",
                "advances.view", "advances.create", "advances.allocate", "advances.submit", "advances.approve", "advances.reject", "advances.post", "advances.export", "final-accounts.view", "final-accounts.approve", "final-accounts.export",
                "attachments.view", "attachments.upload", "transactions.correct", "transactions.cancel", "transactions.history", "notifications.view", "attendance.view", "attendance.check-in", "attendance.check-out", "search.view"
            ],
            ["user"] = ["notifications.view", "attendance.view", "attendance.check-in", "attendance.check-out"]
        };

        var requiredCodes = defaults.Values.SelectMany(codes => codes).Distinct().ToArray();
        var seedRoles = await db.Roles.Where(role => defaults.Keys.Contains(role.Name)).ToDictionaryAsync(role => role.Name);
        var permissions = await db.Permissions.Where(permission => requiredCodes.Contains(permission.Code)).ToDictionaryAsync(permission => permission.Code);
        foreach (var (roleName, codes) in defaults)
        {
            if (!newlyCreatedRoles.Contains(roleName) || !seedRoles.TryGetValue(roleName, out var role)) continue;
            var existingGrants = await db.RolePermissions
                .Where(link => link.RoleId == role.Id)
                .ToListAsync();

            foreach (var code in codes)
            {
                if (!permissions.TryGetValue(code, out var permission) || existingGrants.Any(grant => grant.PermissionId == permission.Id)) continue;
                // Initialize new grants with the existing role's approval scope and cap.
                var priorCode = code switch
                {
                    "suppliers.supplies-accept-technical" or "suppliers.supplies-approve-financial" => "suppliers.approve",
                    _ when code != "project-imports.reject" && code.EndsWith(".reject", StringComparison.Ordinal) => code[..^".reject".Length] + ".approve",
                    _ => null
                };
                var priorApproval = existingGrants.Find(grant => grant.Permission.Code == priorCode);
                var grantScope = priorApproval?.Scope ?? ScopeFor(code);
                if (roleName == "project_manager" && (code is "advances.submit" or "suppliers.supplies-accept-technical") && grantScope == "all")
                    grantScope = "assigned";
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id, PermissionId = permission.Id,
                    Scope = grantScope,
                    AmountLimit = priorApproval?.AmountLimit
                });
            }
        }
    }

    private static string ScopeFor(string code) =>
        code.StartsWith("reports.", StringComparison.Ordinal) ||
        code.StartsWith("projects.", StringComparison.Ordinal) ||
        code.StartsWith("suppliers.", StringComparison.Ordinal) ||
        code.StartsWith("advances.", StringComparison.Ordinal) ||
        code.StartsWith("final-accounts.", StringComparison.Ordinal) ||
        code is "project-imports.approve" or "project-imports.reject"
            ? "all"
            : "assigned";
}
