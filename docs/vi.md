# Nền tảng công cụ tác giả — bộ dùng chung của các cửa sổ chỉnh sửa

`tools_base` là thư viện cho tác giả mod viết cửa sổ chỉnh sửa. Bản thân nó không thêm cửa sổ hay nút nào lên thanh công cụ; người chơi không cần cài riêng: mỗi trình sửa dùng nó sẽ kéo nó theo như một phụ thuộc.

## Ai dùng nó

Các trình sửa cảnh, kịch bản, bản đồ thế giới, tương tác, ánh sáng, Scene Builder, Catalog Export, trình sửa vật phẩm/nhân vật, `rpg_editor`, `rpg_object_editor`, `schema_tools` dùng thư viện này.

## Nó cung cấp gì cho một công cụ

- **`ToolUi`** — giao diện chung của cửa sổ công cụ: nút, trường, mục, đường phân cách, dòng trạng thái, nhãn đã dịch (`ToolUi.Tr`). Khung cửa sổ (`ToolWindow`) vẫn ở `ui_base`.
- **Widget** — `ModFileSelector` (chọn mod ghi được và tệp trong một thư mục), `FilteredList`, `CollapsibleSection`, `BackgroundCanvas` (nền cảnh có thu phóng), `DragHandle`.
- **Tệp** — `ModFileService` (đọc/ghi tệp theo đường dẫn trong mod, chỉ mod dạng thư mục), `ModResourceService`, `SceneFileService`/`SceneBackgroundService` (cảnh logic và nền của chúng), `LocaleFileService` và `PropertiesText` (sửa văn bản `.properties` ở mọi ngôn ngữ của mod mà không mất chú thích và thứ tự).
- **Trình sửa thực thể** — `EntityEditorWindow`, `EntityDocument`, `EntityField`: danh sách có tìm kiếm và biểu mẫu với văn bản ở mọi ngôn ngữ, hình ảnh và trường JSON. Trình sửa vật phẩm và nhân vật được dựng trên chúng (xem `docs/concepts/tools.md`).
- **Biểu mẫu theo lược đồ** — trình sửa trường sinh ra từ chú thích của `schema_base` (giá trị đơn, danh sách, từ điển, văn bản bản địa hóa).
- **Giao ước giữa các công cụ** — `IScenarioResourceEditor` cho phép một cửa sổ nhờ trình sửa kịch bản mở tệp theo địa chỉ trong mod; `IScenarioTextTarget` và `ScenarioTextTargets` đưa phần chèn từ bảng lệnh vào kịch bản được focus gần nhất. Trình sửa tự hủy đăng ký khi cửa sổ đóng.

- **Mục của mod khác** — danh sách có thể hiện các mục của các mod phụ thuộc. Sửa một mục như vậy được ghi thành bản vá trong mod đang sửa; tệp của chủ sở hữu không đổi. Huy hiệu ở trường cho biết giá trị đến từ đâu (bản vá của bạn, bản vá của mod khác, hoặc kế thừa); **↺** đặt lại trường.
- **Trường chỉ đọc** — `EntityField.Locked` hiển thị trường không cho đổi, dành cho giá trị mà thực thể tính từ nơi khác (máu nhân vật khi có chỉ số).
- **Hợp đồng `IMapPickupEditor`** — nút «Place on map» của trình sửa vật phẩm yêu cầu Trình chỉnh sửa bản đồ RPG mở với một vật thể nhặt sẵn sàng đặt.

## Viết công cụ trên nó

Phụ thuộc `tools_base>=0.1.0`, cài đặt `IToolProvider` (dựng cửa sổ) và, nếu cần mục trong hộp công cụ, `IToolBoxButton`. Trò chơi tự tìm chúng trong assembly của mod; không cần ghi gì vào manifest. Chỉ ghi tệp qua `ModFileService`, khi đó mod đóng gói vẫn chỉ đọc.

## Phụ thuộc

`ui_base`, `schema_base`. Bộ kiểm thử `isolation` (`--itest=tools_isolation`) kiểm tra rằng nó chạy được khi không cài cửa sổ chỉnh sửa nào.

## Biểu mẫu và đổi tên

Biểu mẫu `schema_base` có `pick` chọn giá trị từ thực thể khác và `stages` sửa bước nhiệm vụ đa ngữ (`StageFieldEditor`, `ITextFieldEditor`). `EntityRename`, `EntityRenameDialog` trình bày kế hoạch trước khi đổi ID; `SchemaReferenceScanner` tìm tham chiếu do schema và DSL mô tả. Chỉ ghi lại mod chọn, liệt kê tham chiếu ngoài; lưu sửa đổi đang chờ trước và Undo rename khôi phục tệp gốc. `IScenarioResourceEditor`, `IScenarioTextTarget`, `ScenarioTextTargets` nối cửa sổ và chèn vào tài liệu vừa được lấy nét.
