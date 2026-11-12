# BIM Model Comparison — Phase 3

Add-in cho **Autodesk Revit 2023**, C#, **.NET Framework 4.8**, WPF, Windows x64. Phase 3 bổ sung highlight thay đổi trong active view và khôi phục đầy đủ original element overrides. Geometry, MEP systems/dimensions và connectors của Phase 2 vẫn được giữ.

## Build và cài đặt

Mở `MEP_Compare_Project_From_JSON_ver1.sln` bằng Visual Studio 2022, cài workload .NET desktop development và .NET Framework 4.8 Developer Pack. File `.slnx` cũ vẫn được giữ, dùng `.sln` cho VS2022.

```powershell
dotnet build .\MEP_Compare_Project_From_JSON_ver1.sln -c Release -m:1 -p:UseSharedCompilation=false
```

References dùng `RevitAPI.dll`, `RevitAPIUI.dll` và `Newtonsoft.Json.dll` đi kèm Revit 2023, Copy Local=false cho add-in. Nếu Revit cài vị trí khác, thêm `-p:RevitInstallDir="D:\Autodesk\Revit 2023"`. Không cần NuGet/network và không phân phối DLL Autodesk.

DLL mới: `bin\Release\net48\MEP_Compare_Project_From_JSON_ver1.dll`. Với Add-In Manager, load DLL này rồi chạy **BimComparison.Commands.CompareModelsCommand**. Nếu DLL cũ vẫn trong bộ nhớ, khởi động lại Revit trước khi nạp bản mới.

Cài Ribbon cố định: đóng Revit rồi chạy `Install.ps1`. Script chép DLL vào `%APPDATA%\Autodesk\Revit\Addins\2023\BIMModelComparison` và tạo manifest có assembly path tuyệt đối. Mở lại Revit; panel **BIM Model Comparison** trong tab Add-Ins có Export Model JSON, Compare RVT Models, Highlight Changes, Export Comparison Report và Clear Highlights. Chưa tự chạy installer trong quá trình phát triển. Gỡ cài đặt khi Revit đóng bằng cách xóa manifest `BIMModelComparison.addin` và thư mục cài đặt đó.

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
Commands/HighlightCommands.cs  Highlight/Clear Ribbon commands
Commands/ValidateHighlightsCommand.cs Optional reversible Revit integration test
Core/Models.cs                 Schema 1/2 DTOs, settings, reports
Core/HighlightPalette.cs        Status/classification colors
Visualization/HighlightService.cs Transactional overrides + persistent recovery ledger
Visualization/OverrideState.cs All editable Revit 2023 override properties
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
Tests/Phase3Tests.cs             Color policy tests
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

Xem `VALIDATION.md` cho kết quả và manual acceptance. Chưa kiểm thử extraction với RVT thật trong Revit tương tác; cần xác nhận trước khi dùng trên dự án. Phase 3 đã triển khai; native integration checks cần chạy trong Revit theo hướng dẫn bên dưới.


## Sử dụng Phase 3: Highlight và Clear

1. Mở đúng Model B revision và chọn plan, section hoặc 3D view hỗ trợ graphics overrides. So sánh RVT/JSON như trước; không phải export lại JSON chỉ để dùng Highlight.
2. Trong dashboard bấm **Highlight all changes**. Nút này dùng toàn bộ kết quả so sánh, không chỉ các dòng đang lọc/search. Các highlights trước đó do tool tạo trong active view được restore trước khi áp dụng bộ kết quả mới, tránh để sót màu của lần so sánh cũ.
3. Hoặc chọn một dòng Added/Modified rồi bấm **Highlight selected element**. Cách này bổ sung một element vào bộ highlight hiện tại, giữ original override từ lần đầu; không ghi đè backup bằng màu highlight của lần trước.
4. Bấm **Close and inspect view** để đóng modal dashboard và xem model. Mở lại CompareModelsCommand sẽ hiện kết quả gần nhất của phiên Revit.
5. Bấm **Clear Highlights** trên Ribbon, chạy **ClearHighlightsCommand** bằng Add-In Manager, hoặc dùng nút trong dashboard. Clear chỉ khôi phục các element do tool đã highlight trong **active view**, không xóa toàn bộ user-defined overrides của view và không cần còn comparison report.

Màu mặc định: Added xanh lá, Modified vàng. Unchanged không có override. Bật **Classify modified colors** để GeometryChanged cam, LocationChanged/RotationChanged xanh dương, MEPSystemChanged tím. Một element chỉ có một màu: ưu tiên geometry > location/rotation > MEP system > vàng cho các thay đổi còn lại. Ribbon HighlightChangesCommand dùng màu mặc định; dashboard có tùy chọn classification.

**Deleted:** không tồn tại trong Model B nên không đặt override đỏ lên một element khác có integer ID giống nó. Tool báo số Deleted và giữ chi tiết trong report/source model. Chưa tạo temporary comparison geometry và chưa có source-model highlighting.

### Khôi phục graphics

Trước khi tô, tool lấy `View.GetElementOverrides`, lưu tất cả editable properties của OverrideGraphicSettings Revit 2023 (line colors/patterns/weights, cut/surface foreground/background fill colors/patterns/visibility, transparency, halftone, detail level). Highlight bắt đầu từ bản copy của current overrides rồi đặt line colors + solid foreground fills, transparency 0 và halftone false. Nếu thiếu drafting solid fill, chỉ tô line colors và hiển thị thông báo.

Backup JSON được gắn vào chính view bằng Extensible Storage schema `20B00BB2-DBB3-4864-B80C-DB09EB227D8B`, map theo element UniqueId. Graphics changes và backup nằm trong cùng transaction. Clear đọc backup, restore đầy đủ original state rồi xóa entity trong cùng transaction. Nếu thao tác thất bại, transaction rollback; không bỏ backup sau một lần restore chưa thành công. Nếu element đã bị xóa, bỏ qua và báo missing; không tìm phần tử thay thế bằng integer ID.

Backup không phụ thuộc instance HighlightService hoặc Session.LastReport. Nếu người dùng save model trong lúc đang highlight, backup và màu highlight được save cùng view; có thể đóng/mở lại file và dùng Clear. Tool không tự save hoặc synchronize model. Undo/Redo của transaction cũng tác động đồng thời màu và backup. Mỗi view có recovery data riêng; muốn Clear ở view khác phải chuyển sang view đó rồi chạy Clear.

Lưu ý vận hành:

- Highlight/Clear là thay đổi view trong Model B và sẽ đánh dấu document modified. Cần quyền sửa/borrow view và elements theo Revit; read-only/unsupported view bị từ chối. Lỗi permission hoặc SetElementOverrides làm rollback cả batch, không để batch nửa chừng.
- Màu ban đầu nghĩa là element overrides **trước lần highlight đầu tiên đang được theo dõi**. Nếu chỉnh tay overrides của cùng element trong lúc đang highlight, Clear vẫn restore backup ban đầu, không giữ chỉnh sửa tạm đó. Category/filter/material settings không bị sửa.
- Hidden elements, category visibility, section box, phase/design-option visibility, view template, visual style, occlusion và temporary view modes có thể khiến không nhìn thấy màu. Tool không unhide hoặc thay các setting này. Số Highlighted là số overrides đã áp dụng, không đảm bảo mọi element đang xuất hiện trên màn hình. Revit selection có thể che màu highlight; bỏ selection để xem.
- Full override recovery giữ các pattern ElementIds của document. Nếu pattern được xóa sau khi highlight, restore có thể thất bại và transaction giữ nguyên backup; không tự thay original pattern bằng pattern khác. Clear trước khi xóa patterns hoặc dùng Undo để khôi phục pattern bị xóa.
- Schema snapshot vẫn là 2.0 vì Phase 3 không đổi dữ liệu model export. Recovery schema của graphics độc lập với snapshot schema. Giới hạn nhận diện đúng revision B của ProjectInformation.UniqueId vẫn áp dụng.

### Native integration test tùy chọn

Build có thêm **BimComparison.Commands.ValidateHighlightsCommand** để chạy bằng Add-In Manager; không đặt trên Ribbon sản phẩm. Mở model thử nghiệm, active view hỗ trợ overrides, chọn một model element rồi chạy command. Nó tạo original overrides có màu/transparency/halftone/lineweight tùy chỉnh, kiểm tra Highlight thay đổi màu, highlight lặp không mất original, Clear restore đầy đủ, Clear lặp không tác động, và replace bằng result rỗng dọn màu cũ. Các thao tác được bọc TransactionGroup và rollback toàn bộ, kể cả trạng thái highlight đã có trước test.

Command test đã compile nhưng **chưa được chạy trong Revit trong phiên phát triển này**. Save/close/reopen, Undo/Redo, nhiều view và quyền worksharing cần xác nhận thủ công theo VALIDATION.md.
