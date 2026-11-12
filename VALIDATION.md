# Validation — Phase 3

Target: Revit 2023, .NET Framework 4.8, x64. Build bằng DLL Revit 2023 đang cài trên máy, không dùng API giả. Core tests không load Revit runtime; UI regression chạy STA với WPF CollectionView thật, không mở Revit UI.

## Automated checks

Commands tại README. Lần phát triển Phase 2 ngày 2026-11-12:

- Release solution build thành công, 0 warnings / 0 errors.
- Core: **43/43 checks passed** (18 Phase 1, 25 Phase 2).
- UI/ViewModel: **7/7 regression checks passed**; UI test project và add-in build 0 warnings / 0 errors.
- Synthetic 100,000-element common-scope comparison: 220 ms trong lần chạy ghi nhận; element ít thuộc tính, không gồm Revit API extraction, geometry triangulation, JSON I/O hoặc WPF rendering.

Phase 1 coverage: 4 trạng thái; nhiều changes; ordering/formatting; numeric/rotation tolerances; parameter key cùng tên; integer ElementId reassignment; family type; duplicate keys/IDs; schema/scope validation; finite numbers; cancel; vi-VN culture; JSON roundtrip và CSV details.

Phase 2 coverage:

1. Identical advanced data.
2. Triangle vertex/primitive order, reversed curve, systems/connectors order.
3. Canonical exact fingerprint.
4. Geometry within tolerance với hash khác.
5. Shape change giữ nguyên bounding box/volume/area.
6. Curve interior change giữ nguyên endpoints.
7. Area/volume tolerance.
8. System type change.
9. System rename.
10. Dedicated MEP dimension change.
11. Connector origin/size within tolerance.
12. Connector direction reversal.
13. Connector resize.
14. Connected owner change.
15. Connector count change.
16. Connected port đổi trên cùng owner.
17. Partial extraction classified ExtractionIncomplete.
18. Missing geometry rejected.
19. Geometry mode mismatch rejected.
20. Nonfinite geometry rejected.
21. Legacy JSON không giả định advanced data.
22. Mixed-schema common-scope comparison.
23. Lightweight volume comparison.
24. Ambiguous candidate bipartite matching.
25. Advanced JSON roundtrip và multi-classification report.

Samples/ModelA/B là dữ liệu Phase 1 tổng hợp, mỗi status 1 element. Samples/Phase2/ModelA/B là một matched pipe có GeometryChanged, MEPSystemChanged và ConnectorChanged; JSON/CSV report đi kèm. Không phải dữ liệu xuất từ RVT thật.

UI regression bao gồm result population, status/search filters, preserve sorting khi display lại, empty report, Phase 2 defaults và legacy scope toggles. Cách nạp collection tạo view riêng rồi thay binding một lần, không mutate bound source trong DeferRefresh.

## Manual Revit acceptance — chưa thực hiện

Chưa có cặp RVT thử nghiệm được cung cấp và chưa chạy extraction Phase 2 trong Revit tương tác. Build với DLL thật và pure-data tests không chứng minh mọi API property hoạt động với mọi family/category. Cần kiểm tra:

1. Nạp DLL mới, mở CompareModelsCommand, xác nhận title Phase 2 và ba advanced checkboxes/mode/tolerance.
2. Tạo A gồm pipes, ducts, fitting, accessory, equipment, electrical equipment, cable tray, conduit, generic family; save thành B để giữ UniqueIds.
3. B: resize pipe/duct; move/rotate family; sửa geometry family/type; đổi arc shape có endpoints giống; thêm/xóa element. Export Detailed cùng settings. Kiểm tra classifications/old-new values và số đếm.
4. Đổi MEP system name/type/membership; thử equipment nhiều systems và electrical circuits. Kiểm tra Systems list và MEPPropertyChanged độc lập với Parameters checkbox.
5. Connect/disconnect, nối lại sang owner khác/cổng khác của cùng owner, rotate connector family, resize connector. Kiểm tra origin/direction/size/references; không có đổi giả chỉ do thứ tự connector.
6. Nested/mirrored/rotated family instances phải dùng đúng world transform. So sánh cùng model export hai lần, kiểm tra fingerprint và không có đổi giả.
7. Test Lightweight rồi Detailed, kiểm tra warning và không so sánh chéo mode. Test geometry vượt primitive limit/unsupported objects phải có Partial + ExtractionIncomplete và errors.
8. Test JSON Phase 1 tắt advanced → so sánh được; bật advanced → báo cần export lại. Không tự tạo số 0 thay dữ liệu thiếu.
9. Test Workshared A central/local, A đã mở, newer/corrupted/access-denied file và missing links. A không được save, luôn close khi mở bởi tool; không đóng document người dùng đã mở.
10. Test B có worksets đóng/unsaved edits; report phải được hiểu theo trạng thái đang mở. Links không được trộn với host elements.
11. Test select/zoom/report JSON/CSV, search classification, rerun comparison trên cùng dashboard, Cancel JSON và closing khi task đang chạy.
12. Profile RVT thật: thời gian geometry extraction, memory Detailed snapshot/report, UI rendering; synthetic 100k test không thay thế bước này.

Highlight/Clear đã có trong Phase 3; native acceptance bổ sung bên dưới.


## Phase 3 validation

Release solution build: 0 errors / 0 warnings. Core **51/51** checks passed, gồm 43 checks trước và 8 color-policy checks mới. Color tests bao gồm Added/Deleted/Unchanged, Modified mặc định, classification priority và toggle. Đây không phải native graphics/transaction test.

Optional `ValidateHighlightsCommand` được build với RevitAPI 2023 thật, bao gồm 4 native checks chạy trong TransactionGroup rồi rollback: actual override thay đổi, repeated highlight + full original recovery qua service instance mới, repeated Clear no-op, replacement empty result phục hồi stale colors. Command **chưa được chạy** trong Revit tương tác trong phiên phát triển này.

Manual acceptance cần thực hiện:

1. Plan/section/3D: Added xanh, Modified vàng; classification cam/xanh dương/tím theo priority. Kiểm tra projection và cut fills/line colors. Deselect element trước khi đánh giá màu.
2. Trước highlight, đặt overrides riêng gồm custom color, line pattern/weight, cut/surface foreground/background fill pattern + visibility, transparency, halftone, detail level. Highlight → Clear phải khôi phục tất cả properties, không chỉ màu.
3. Highlight row nhiều lần, thay classification, rồi Highlight all một comparison khác: original backup không được thành màu highlight và không còn stale overrides của comparison cũ.
4. Highlight trong hai view, Clear từng view phải độc lập. Unchanged/unrelated elements phải giữ overrides riêng. Search/filter không thu hẹp Highlight all.
5. Save khi đang highlight → close/reopen → ClearHighlightsCommand không cần report phải restore. Tool không tự save/sync. Sau Clear, save rồi reopen không còn recovery entity của view đó.
6. Undo/Redo Highlight/Clear: colors và backup phải đồng bộ, Clear sau Undo/Redo vẫn có đúng dữ liệu.
7. Xóa element đang highlight → Clear: báo missing, không restore nhầm element mới. Deleted của comparison chỉ report, không tô nhầm model B.
8. Read-only model, view không hỗ trợ override, view/element không có quyền worksharing: thất bại phải rollback toàn bộ; dữ liệu recovery trước đó không mất.
9. Hide/category/phase/section box/visual style/selection: tool không đổi visibility để cưỡng ép hiện màu. Highlight count là overrides applied.
10. Chỉnh tay override khi đang highlight → Clear phải trở về trạng thái trước lần highlight đầu. Xóa referenced pattern sau highlight có thể làm restore thất bại; giữ backup và không tự bỏ original pattern.
11. Add-In Manager chạy CompareModelsCommand rồi dùng các nút Highlight/Clear ngay trong cùng dashboard. Ribbon cố định cần cài Application manifest và restart Revit.
12. Chạy ValidateHighlightsCommand trên model thử; xác nhận thông báo 4 checks passed và không còn test modifications sau rollback.

Chưa có runtime acceptance với RVT thật. Không kết luận khả năng restore đã được kiểm chứng trong Revit chỉ từ build và color-policy tests.
