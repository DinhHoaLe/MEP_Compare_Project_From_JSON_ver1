# BIM Model Comparison — Phase 2

Add-in cho **Autodesk Revit 2023**, C#, **.NET Framework 4.8**, WPF, Windows x64. Phase 2 bổ sung geometry, MEP systems/dimensions và connectors vào bộ so sánh Phase 1. Chưa có Highlight/Clear của Phase 3.

## Build và cài đặt

Mở `MEP_Compare_Project_From_JSON_ver1.sln` bằng Visual Studio 2022, cài workload .NET desktop development và .NET Framework 4.8 Developer Pack. File `.slnx` cũ vẫn được giữ, dùng `.sln` cho VS2022.

```powershell
dotnet build .\MEP_Compare_Project_From_JSON_ver1.sln -c Release -m:1 -p:UseSharedCompilation=false
```

References dùng `RevitAPI.dll`, `RevitAPIUI.dll` và `Newtonsoft.Json.dll` đi kèm Revit 2023, Copy Local=false cho add-in. Nếu Revit cài vị trí khác, thêm `-p:RevitInstallDir="D:\Autodesk\Revit 2023"`. Không cần NuGet/network và không phân phối DLL Autodesk.

DLL mới: `bin\Release\net48\MEP_Compare_Project_From_JSON_ver1.dll`. Với Add-In Manager, load DLL này rồi chạy **BimComparison.Commands.CompareModelsCommand**. Nếu DLL cũ vẫn trong bộ nhớ, khởi động lại Revit trước khi nạp bản mới.

Cài Ribbon cố định: đóng Revit rồi chạy `Install.ps1`. Script chép DLL vào `%APPDATA%\Autodesk\Revit\Addins\2023\BIMModelComparison` và tạo manifest có assembly path tuyệt đối. Mở lại Revit; panel **BIM Model Comparison** trong tab Add-Ins có Export Model JSON, Compare RVT Models, Export Comparison Report. Chưa tự chạy installer trong quá trình phát triển. Gỡ cài đặt khi Revit đóng bằng cách xóa manifest `BIMModelComparison.addin` và thư mục cài đặt đó.

## Sử dụng Phase 2

1. Chọn cùng category cho hai model. Bật **Geometry**, **MEP systems / dimensions**, **Connectors** theo phạm vi cần so sánh.
2. Geometry chọn **Detailed** để so sánh tessellated shape; **Lightweight** nhanh và gọn hơn nhưng có thể bỏ sót shape giữ nguyên các đại lượng tổng hợp.
3. Export lại JSON của cả A và B bằng DLL Phase 2. Snapshot mới có `SchemaVersion=2.0`, scope `Phase2-Geometry-MEP-Connectors`.
4. Browse A/B tới hai JSON rồi **Compare A/B JSON**. Hoặc mở B, Browse A tới RVT cũ, bấm **Compare A RVT → active B**.
5. Xem Change type trong bảng; search có thể nhập `GeometryChanged`, `MEPSystemChanged`, `MEPPropertyChanged`, `ConnectorChanged`, `ExtractionIncomplete`. Sort bằng column header, filter theo Status.
6. Export JSON/CSV report để xem old/new values. Một element có thể có nhiều dòng property change; dashboard đếm element.

**JSON Phase 1 không chứa geometry/MEP/connectors.** Muốn đọc lại JSON cũ, tắt ba checkbox mới. Không tự suy diễn dữ liệu chưa export. Hai schema có thể so sánh trong phạm vi chung, nhưng category phải trùng và các dữ liệu được yêu cầu phải có sẵn. Geometry modes của hai snapshot và setting hiện tại phải khớp. Tolerance có thể đổi vì snapshot lưu dữ liệu raw, không lượng tử hóa theo tolerance.

## Dữ liệu và thuật toán

### Geometry

`Extraction/GeometryExtractor.cs` lấy geometry tại Fine detail, không lấy non-visible objects, không phụ thuộc active view. Duyệt nested GeometryInstance bằng symbol geometry và transform tích lũy, đưa tọa độ về internal world coordinates. Trích xuất:

- Solid count, mesh count, curve count; solid volume (ft³), surface area (ft²), standalone curve/polyline length (ft).
- Bounds và center là **midpoint của bounds**, không phải center of mass. CurveLength không bao gồm toàn bộ cạnh của solid.
- Detailed: face triangles, mesh triangles và curve tessellation; point/polyline cũng được ghi nhận. Triangulation của solid chỉ dùng cho shape, không cộng diện tích lần thứ hai. Standalone mesh area được tính từ triangles; không suy diễn volume của open mesh.
- Lightweight: solid measures và transformed solid bounds, không lưu face triangles. Standalone meshes vẫn cần duyệt triangle để tính diện tích/bounds.
- SHA-256 canonical fingerprint sắp xếp triangle vertices, triangles và curves, chuẩn hóa hướng curve. Hash dùng số raw invariant và chỉ là đường tắt cho trường hợp khớp tuyệt đối; khác hash **không tự động** coi là shape changed.
- Khi hash khác, detailed comparison dùng spatial buckets và triangle matching theo tolerance; curve matching không phụ thuộc thứ tự và chấp nhận reverse direction. Matching một-một bảo toàn multiplicity, tránh phụ thuộc thứ tự candidates.

Tolerance length nhập mm được đổi sang feet. Area và volume có tolerance riêng theo ft²/ft³. Không chỉ so sánh bounding box. Shape có thể thay đổi trong khi volume/area/bounds giữ nguyên và vẫn được phát hiện ở Detailed.

Giới hạn: đây là so sánh **tessellation**, không phải exact BRep equivalence. Cùng bề mặt nhưng đổi tessellation/topology có thể bị báo GeometryChanged. Curve tessellation có thể bỏ sót thay đổi giữa sample points. Hai mode dùng cách lấy bounds khác nhau và không so sánh chéo mode. Di chuyển hoặc xoay cũng thay đổi world geometry; một element có thể có cả GeometryChanged và LocationChanged. Unsupported geometry, nonconformal transforms, lỗi API hoặc quá 20.000 primitives/element được đánh dấu Partial, ghi lỗi, không âm thầm coi là đầy đủ.

### MEP systems và dimensions

`Extraction/MEPSystemExtractor.cs` đọc MEPCurve.MEPSystem, connector systems và FamilyInstance.MEPModel.GetElectricalSystems khi có. Hỗ trợ nhiều system trên equipment. System được nhận diện bằng UniqueId, name, type UniqueId/name; không dựa vào thứ tự.

Dimensions có sẵn được lấy bằng BuiltInParameter: pipe diameter, duct width/height/diameter và element length, theo internal feet. Connector kích thước cũng được đọc riêng, hỗ trợ family/fittings mà parameter dimensions không hiện trực tiếp. MEP system metadata thay đổi được phân loại `MEPSystemChanged`; dimensions phân loại `MEPPropertyChanged`.

### Connectors

Đọc ConnectorManager của MEPCurve hoặc FamilyInstance.MEPModel: domain, connector type, shape, origin, direction (BasisZ đơn vị), round radius hoặc rectangular/oval width/height, referenced owners và connected-port origins khi truy cập được.

Connector matching không dùng connector enumeration order hoặc connector ID không đảm bảo ổn định qua revision. So sánh một-một theo origin, hướng, size, type/domain/shape và topology. Origin/size dùng length tolerance; hướng dùng angle tolerance theo radian. Owner references chuẩn hóa bằng UniqueId; connected-port origin giúp phát hiện chuyển sang cổng khác trên cùng owner. Logical connectors không có physical coordinate/shape/size; null ở đây là có chủ đích. AllRefs có thể gồm logical references; report giữ domain/type để người dùng phân biệt.

Generic element không có ConnectorManager ghi `NotApplicable`, khác với extraction failure `Partial`. Partial data tạo classification `ExtractionIncomplete` trên matched elements kể cả khi hai bên cùng Partial; không được phân loại Unchanged chỉ vì lỗi giống nhau. Snapshot/report cũng ghi ExtractionErrors. Không phải mọi system relationship của add-in bên thứ ba đều được Revit API công khai.

## Quy tắc chung và giới hạn vận hành

- Exact match theo Element.UniqueId, dictionary O(n). Recreated elements vẫn Added/Deleted. Chưa có probable replacement matching; không tự gộp các phần tử khác UniqueId.
- Identity: category, family/type, TypeUniqueId, workset name, design option/phase UniqueIds. Integer ElementId chỉ phục vụ hiển thị, không quyết định match xuyên phiên bản.
- Parameters: khóa scope instance/type + BuiltInId, shared GUID hoặc ParameterElement.UniqueId. Type parameters được gắn trên instances. Raw values là cơ sở so sánh, display formatting/units/order không gây thay đổi giả. Các đại lượng ngoài length/angle dùng absolute NumberTolerance trong internal units.
- Host instances trong 12 category ban đầu; không traverse linked models. Danh sách links và trạng thái loaded ghi vào warnings. Active B phản ánh trạng thái đang mở và cả thay đổi chưa save; worksets đóng có thể làm khác phạm vi dữ liệu.
- RVT A mở bằng Application.OpenDocumentFile không activate, workshared mở detached preserve worksets; đóng trong finally bằng Close(false). Không mở transaction hoặc save model nguồn. Nếu A đã mở, dùng JSON workflow hoặc đóng A trước. Newer versions và lỗi mở/central-local/access được báo; Revit có thể hiện dialog liên quan links/worksharing, không bypass.
- Modal WPF giữ API actions trên Revit thread trong command; không cần ExternalEvent ở thiết kế này. JSON-only chạy background và có cancel giữa các bước/vòng lặp. RVT extraction vẫn đồng bộ; UI có thể không repaint progress trong lúc Revit bận. Chưa có cancel tương tác giữa extraction.
- Snapshot và results giữ trong RAM; streaming JSON I/O không đồng nghĩa disk-backed comparison. Detailed geometry và reports có thể lớn, nên giới hạn category hoặc dùng Lightweight nếu cần. Synthetic benchmark không đo Revit extraction/triangulation.
- Deleted chỉ trong report vì không còn trong B. Select/Zoom cần mở đúng model B, kiểm tra project identity và tìm element bằng UniqueId. Các revision cùng project có thể cùng ProjectInformation.UniqueId; không dùng kiểm tra này để xác nhận chính xác file revision.
- CSV UTF-8 có escape quote/comma/newline và thêm apostrophe vào strings có thể bị hiểu là spreadsheet formula. Report gần nhất chỉ lưu trong bộ nhớ phiên Revit.

## Source layout

```text
App.cs                         Ribbon + session result
Commands/Commands.cs           Compare, Export JSON, Export Report, availability
Core/Models.cs                 Schema 1/2 DTOs, settings, reports
Core/ModelComparisonEngine.cs   Identity/parameters/location + scope validation
Core/GeometryComparison.cs     Canonical fingerprint, tolerant shape matching
Core/AdvancedComparison.cs     Geometry/MEP/connector comparisons, validation
Core/JsonSerializerService.cs   Streaming JSON; temp file then replace
Core/ComparisonReportService.cs JSON/CSV reports
Extraction/RevitModelExtractor.cs Category collector, parameters, extraction orchestration
Extraction/GeometryExtractor.cs Revit geometry traversal
Extraction/MEPSystemExtractor.cs Revit systems/dimensions/connectors
Extraction/ModelWorkflow.cs     Open/extract/close RVT A, extract active B
UI/ComparisonWindow.xaml        Phase 2 dashboard
UI/ComparisonWindow.xaml.cs     API/dialog orchestration
UI/ComparisonViewModel.cs       Filtering, sorting, settings, result projection
Tests/Program.cs                Phase 1 tests + Phase 2 entry
Tests/Phase2Tests.cs             Advanced comparison tests
Tests/UiRegression.cs           WPF view regression and Phase 2 settings
Samples/Phase2/                 Synthetic geometry/MEP/connector snapshots and report
BIMModelComparison.addin
Install.ps1
VALIDATION.md
```

## Kiểm thử

```powershell
dotnet build .\MEP_Compare_Project_From_JSON_ver1.sln -c Release -m:1 -p:UseSharedCompilation=false
.\Tests\bin\Release\net48\CoreTests.exe .\Samples
dotnet build .\Tests\UiRegression.csproj -c Release -m:1 -p:UseSharedCompilation=false
.\Tests\bin\Release\net48\UiRegression.exe
```

Xem `VALIDATION.md` cho kết quả và manual acceptance. Chưa kiểm thử extraction với RVT thật trong Revit tương tác; cần xác nhận trước khi dùng trên dự án. Phase 3 highlighting/restore overrides chưa triển khai.
