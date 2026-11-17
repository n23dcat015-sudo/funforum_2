"# funforum_2" 
## Cài đặt lần đầu (bắt buộc trước khi chạy)

Cần: .NET 8 SDK, Node.js 20+, PostgreSQL 16 (hoặc `docker compose up postgres`).

```bash
cd backend/Funforum.Api
dotnet restore
cp appsettings.Development.json.example appsettings.Development.json   # rồi điền các giá trị thật
dotnet tool install --global dotnet-ef   # nếu chưa có

# Tạo migration đầu tiên: bật extension citext/pg_trgm + tạo bảng users/admins/refresh_tokens
dotnet ef migrations add AddAuthTables

# Áp migration vào DB (cần Postgres đang chạy, đọc connection string từ appsettings.Development.json)
dotnet ef database update
```

⚠️ Nếu lệnh `dotnet ef migrations add` báo lỗi biên dịch, đó là chỗ cần sửa trước —
xem mục "Lưu ý quan trọng" bên dưới.

## Chạy local (không qua Docker) — khuyến nghị khi dev

**Backend:**
```bash
cd backend/Funforum.Api
dotnet run
# API chạy ở http://localhost:5080, Swagger ở /swagger
# Lần đầu chạy sẽ tự tạo tài khoản Super Admin từ SeedSuperAdmin trong
# appsettings.Development.json (xem mục "Cài đặt lần đầu" ở trên để tạo file này)
```

**Frontend:**dotnet ef database update
```bash
cd frontend
npm install
npm run dev
# Chạy ở http://localhost:5173, /api được Vite proxy sang :5080
```

## Chạy bằng Docker Compose (giống production)

```bash
cp .env.example .env   # rồi sửa các giá trị, đặc biệt JWT_SECRET và SEED_SUPERADMIN_*
docker compose up --build
# Frontend: http://localhost:8080
# Backend:  http://localhost:5080/api/health
```
Lưu ý: image backend hiện chưa tự chạy `dotnet ef database update` khi khởi động
(việc đó thuộc Giai đoạn 9 theo roadmap) — với Docker Compose bạn cần chạy migration
thủ công một lần nhắm vào DB trong container trước khi dùng.

**Google Sign-In và email:** điền `GOOGLE_CLIENT_ID` trong `.env` bằng Web Client ID
của Google, khai báo origin frontend trong Google Cloud Console, rồi build lại frontend
với `docker compose up --build`. Cùng ID được dùng để backend kiểm tra token.
Email chào mừng chỉ gửi thật khi `.env` có `SMTP_HOST`, `SMTP_FROM` và cấu hình SMTP
hợp lệ; nếu `SMTP_HOST` để trống, nội dung mail chỉ xuất hiện trong log backend.
Nếu dùng Gmail SMTP, hãy dùng App Password thay vì mật khẩu Gmail thông thường.


## ⚠️ Lưu ý quan trọng

Môi trường tạo ra dự án này **không có .NET SDK** nên toàn bộ code backend được
viết tay, chưa từng chạy `dotnet restore/build/ef` để tự kiểm tra. Khả năng cao
có lỗi nhỏ (version package không khớp, EF Core API tên khác một chút giữa các
bản...). Việc đầu tiên khi nhận code:

```bash
cd backend/Funforum.Api
dotnet restore
dotnet build
```

Sửa lỗi biên dịch nếu có (thường chỉ là version package trong `.csproj`), rồi
mới chạy `dotnet ef migrations add AddAuthTables` như hướng dẫn ở trên. Nếu
`dotnet ef` báo "No database provider has been configured" hoặc tương tự, đảm
bảo `ConnectionStrings:Default` trong `appsettings.Development.json` trỏ đúng
tới một Postgres đang chạy được (extension `citext`/`pg_trgm` được migration tự
bật, không cần cài tay).
"# funforum_2" 
