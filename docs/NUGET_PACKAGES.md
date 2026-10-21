# Framework và NuGet của source bàn giao

Framework: .NET Framework 4.8.1; ASP.NET MVC5; EF6; Identity2. packages.config là danh sách phiên bản chuẩn, không dùng ASP.NET Core.

| Package | Phiên bản | Target |
|---|---|---|
| Antlr | 3.5.0.2 | net481 |
| bootstrap | 5.2.3 | net481 |
| EntityFramework | 6.5.1 | net481 |
| jQuery | 3.7.0 | net481 |
| jQuery.Validation | 1.19.5 | net481 |
| Microsoft.AspNet.Identity.Core | 2.2.4 | net481 |
| Microsoft.AspNet.Identity.EntityFramework | 2.2.4 | net481 |
| Microsoft.AspNet.Identity.Owin | 2.2.4 | net481 |
| Microsoft.AspNet.Mvc | 5.2.9 | net481 |
| Microsoft.AspNet.Razor | 3.2.9 | net481 |
| Microsoft.AspNet.Web.Optimization | 1.1.3 | net481 |
| Microsoft.AspNet.WebPages | 3.2.9 | net481 |
| Microsoft.CodeDom.Providers.DotNetCompilerPlatform | 2.0.1 | net481 |
| Microsoft.jQuery.Unobtrusive.Validation | 3.2.11 | net481 |
| Microsoft.Owin | 4.2.2 | net481 |
| Microsoft.Owin.Host.SystemWeb | 4.2.2 | net481 |
| Microsoft.Owin.Security | 4.2.2 | net481 |
| Microsoft.Owin.Security.Cookies | 4.2.2 | net481 |
| Microsoft.Owin.Security.Facebook | 4.2.2 | net481 |
| Microsoft.Owin.Security.Google | 4.2.2 | net481 |
| Microsoft.Owin.Security.MicrosoftAccount | 4.2.2 | net481 |
| Microsoft.Owin.Security.OAuth | 4.2.2 | net481 |
| Microsoft.Owin.Security.Twitter | 4.2.2 | net481 |
| Microsoft.Web.Infrastructure | 2.0.0 | net481 |
| Modernizr | 2.8.3 | net481 |
| Newtonsoft.Json | 13.0.3 | net481 |
| Owin | 1.0 | net481 |
| WebGrease | 1.6.0 | net481 |

Khôi phục: mở solution trong Visual Studio 2022 với workload ASP.NET và targeting pack 4.8.1; chọn Restore NuGet Packages. Trong Developer PowerShell có thể chạy MSBuild.exe OnlineSalesManagement.sln /t:Restore /p:RestorePackagesConfig=true rồi Build Debug /p:MvcBuildViews=true. Không đóng gói thư mục packages cache hoặc bin/obj.
Trên máy nhận cần truy cập nguồn NuGet được phép. Restore chưa được kiểm chứng trên một máy sạch trong phiên này.
