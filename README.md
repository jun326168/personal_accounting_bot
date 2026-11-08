# Personal Accounting Bot

在 LINE 上用自然語言記帳的聊天機器人。與一般記帳 app 不同，它採用**複式簿記**（double-entry）：每筆交易都是借貸平衡的分錄，已入帳的資料不可修改，只能沖銷更正，這些規則由資料庫層強制執行。

使用者輸入「午餐 120」，系統透過 Claude 解析成分錄提案，以確認卡片讓使用者核對，按下確認後才寫入資料庫。

> 以 C# / ASP.NET Core (.NET 10) 開發的個人專案。規模是個人使用，但資料模型與完整性規則遵循會計的核心原則。

### 與一般記帳 app 的差異

| | 一般記帳 app | 本專案 |
|---|---|---|
| 資料模型 | 一筆金額加分類 | 借貸分錄，至少兩筆且借貸必須平衡 |
| 修改紀錄 | 可直接編輯、刪除 | 不可修改，只能沖銷（保留完整軌跡） |
| 正確性保證 | 靠應用程式 | PostgreSQL trigger 與 constraint |
| 帳戶概念 | 分類標籤 | 資產、負債、權益、收入、費用五類帳戶，各有正常餘額方向 |

<!-- TODO: 放 1~2 張 LINE 對話截圖或一張 demo GIF，這是 README 最重要的部分 -->
<!-- ![demo](docs/demo.gif) -->

## 功能

- **自然語言記帳**：「昨天用信用卡買咖啡 150」、「薪水入帳 50000」
- **確認後才入帳**：AI 只產生「提案」，使用者按下卡片上的按鈕才會寫入（human-in-the-loop）
- **沖銷更正**：已入帳的交易不可修改或刪除，只能以沖銷交易更正
- **查詢**：查餘額、搜尋交易、統計收入與支出
- **複式簿記**：每筆交易至少兩筆分錄，借貸必須平衡

## 技術

| 類別 | 使用 |
|---|---|
| 語言 / 框架 | C#, ASP.NET Core Minimal API, .NET 10 |
| 資料存取 | Entity Framework Core 10, Npgsql |
| 資料庫 | PostgreSQL 17 |
| 外部服務 | LINE Messaging API, Anthropic Claude API (tool use) |
| 部署 | Docker, Docker Compose, Nginx（反向代理、HTTPS）, Let's Encrypt（Certbot）, DigitalOcean |
| CI/CD | GitHub Actions, GitHub Container Registry (GHCR) |

## 架構

```
LINE ──POST /webhook──▶ LineSignatureFilter (驗證 HMAC-SHA256)
                              │
                              ▼
                      WebhookQueue (Channel, 容量 100)  ──▶ 立即回 202
                              │
                              ▼
                      WebhookWorker (BackgroundService)
                              │
                              ▼
                      WebhookDispatcher ──▶ Follow / Message / Postback Handler
                                                   │
                         ┌─────────────────────────┴───────────────┐
                         ▼                                         ▼
                  MessageService                            PostbackService
           (AI tool loop, 最多 6 輪)                    (確認 / 取消提案，寫入交易)
                         │
        ┌────────────────┼───────────────────────────────────────────────┐
        ▼                ▼               ▼              ▼                ▼
   get_balance  search_transactions  summarize  propose_transactions  propose_reversal
```

專案結構：

```
src/PersonalAccountingApi/
├── Webhooks/          接收、佇列、背景處理、事件分派
├── Services/          訊息與 Postback 的業務邏輯
├── Infrastructure/
│   ├── AiTools/       Claude 可呼叫的工具（每個工具一個類別，實作 IAiTool）
│   ├── AiPrompts/     System prompt 組裝
│   ├── ExternalApis/  LINE / Anthropic client（透過介面隔離）
│   └── Data/          DbContext
├── Domain/Entities/   User, Message, Account, Transaction, JournalEntry, Proposal
└── Filters/           LINE 簽章驗證
db/                    SQL migrations 與初始化腳本
```

## 設計重點

**Webhook 處理**
- 驗證 `X-Line-Signature`（HMAC-SHA256，使用 `FixedTimeEquals` 比對）
- Endpoint 只負責入列並回 `202`，實際處理交給 `BackgroundService`，避免 AI 呼叫過慢導致 LINE 逾時重送
- 以 `line_message_id` 唯一鍵與重送檢查，確保同一則訊息不會重複處理
- 單一事件處理失敗不會中斷整個 worker

**AI 工具呼叫**
- 工具定義集中在 `IAiTool`，新增工具只需新增類別並註冊
- 工具回傳分兩種：回饋給模型繼續推理，或結束迴圈（產生提案）
- 提案寫入前由程式再次驗證：帳戶名稱必須存在、金額為正整數、借貸合計相等、開帳只能一次
- 迴圈上限 6 輪，失敗時回傳友善訊息

**資料完整性（在資料庫層保證，而不只靠應用程式）**
- 借貸平衡：`DEFERRABLE INITIALLY DEFERRED` constraint trigger，交易 commit 時檢查
- 分錄與交易不可修改、不可刪除（trigger），更正只能新增沖銷交易
- 沖銷以 `UNIQUE` 約束避免同一筆被重複沖銷
- 每位使用者只能有一筆開帳交易（以 advisory lock 序列化並行寫入）

**併發與冪等**
- 確認卡片以條件式 `UPDATE ... WHERE status = 'pending'` 搶佔，並與寫入分錄放在同一個 transaction，避免重複點擊造成重複入帳；寫入失敗時一併回傳，卡片仍可再次確認

**安全與部署**
- 資料庫分兩種角色：管理者負責 migration，應用程式使用最小權限帳號（僅 `SELECT` / `INSERT` 與少數欄位的 `UPDATE`）
- Container 以非 root 執行，唯讀檔案系統，`cap_drop: ALL`，`no-new-privileges`
- 機密一律由環境變數注入，缺少時應用程式啟動即失敗（`ValidateOnStart`）
- Migration 腳本以單一 transaction 套用並記錄版本

## 設計決策

**帳戶清單直接嵌入 System Prompt，不做 `search_account` 工具**
帳戶是預先定義的固定清單，所以直接放進 prompt。這樣少一次 tool call 的往返，模型也只能從清單選擇，降低亂編帳戶名稱的機會；程式端仍會再驗證名稱是否存在。Prompt 中穩定內容（規則、帳戶）放前面，每次變動的日期放最後，讓前綴可被 prompt caching 重複利用。
<!-- TODO: 補上你當時考慮過的其他方案與取捨，例如帳戶數量變多時會怎麼改 -->

**AI 只提案，不直接寫入**
LLM 可能判斷錯誤。因此 AI 工具只能產生 `Proposal`，使用者按下確認後，程式才寫入交易。System prompt 也禁止模型宣稱「已記錄」。

**不可變的帳本，以沖銷取代修改**
會計上已入帳的紀錄不應被改寫，否則無法稽核。這裡以資料庫 trigger 禁止更新與刪除分錄，更正一律新增一筆反向的沖銷交易，並以 `UNIQUE` 約束避免重複沖銷。

**規則放在資料庫，而不只是應用程式**
借貸平衡以 deferred constraint trigger 檢查，因此即使應用程式有 bug，或模型產生了錯誤分錄，不平衡的交易也無法 commit。

**AI 的輸出一律視為不可信輸入**
工具在寫入提案前會重新驗證帳戶名稱、金額、借貸平衡與開帳次數；沖銷時會再次確認該交易屬於目前使用者，不信任儲存在提案裡的 id。

## 快速開始（本機開發）

需求：Docker、LINE Messaging API channel、Anthropic API key

```bash
git clone https://github.com/jun326168/personal_accounting_bot.git
cd personal_accounting_bot
cp .env.example .env     # 填入 LINE 與 Anthropic 金鑰、資料庫帳密
docker compose up -d --build
```

服務監聽 `127.0.0.1:8080`。LINE webhook 要求公開的 HTTPS 網址，本機開發使用 ngrok 建立通道：

```bash
ngrok http 8080
```

到 LINE Developers Console 將 Webhook URL 設為 `https://<ngrok 網址>/webhook`，並啟用 Use webhook。

健康檢查：`GET /health`

## 部署與 CI/CD

```
git push (main)
      │
      ▼
GitHub Actions ── build ──▶ 建置 Docker image，以 commit SHA 為 tag 推送到 GHCR
      │
      └─ deploy ──▶ SSH 到 DigitalOcean 伺服器
                       ├─ git pull
                       ├─ docker compose pull api
                       └─ docker compose up -d   （db-migrate 自動套用新的 migration）

Internet ──443──▶ Nginx（TLS 終止、HTTP 轉 HTTPS）──▶ api:8080
                       ▲
                  Certbot（每 12 小時檢查並自動續期 Let's Encrypt 憑證）
```

- **開發環境**：ngrok 提供 HTTPS，直接把本機服務接到 LINE
- **正式環境**：Nginx 反向代理，API 只綁定 127.0.0.1，對外只開放 Nginx 的 80 / 443
- **自動部署**：推送到 `main` 即自動建置並部署，不需要手動登入伺服器
- **映像檔以 commit SHA 標記**：每次部署的版本可追溯，需要回復時指定舊的 SHA 即可
- **Compose 分層**：`docker-compose.yml` 為共用設定，`docker-compose.prod.yml` 疊加 Nginx、Certbot 與預建映像檔
- 部署所需的機密（伺服器位址、帳號、SSH 金鑰）存放於 GitHub Secrets

<!-- TODO: 補一句首次部署如何取得第一張憑證（例如先用 HTTP 啟動 Nginx 跑 certbot certonly），這是面試常被問到的細節 -->

正式環境額外需要 `API_IMAGE` 與 `DOMAIN` 兩個環境變數。

### 環境變數

| 變數 | 說明 |
|---|---|
| `LINE_CHANNEL_SECRET` | LINE channel secret（驗證簽章） |
| `LINE_CHANNEL_ACCESS_TOKEN` | LINE channel access token |
| `ANTHROPIC_API_KEY` | Anthropic API key |
| `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` | 資料庫管理者帳號（僅用於 migration） |
| `APP_DB_USER` / `APP_DB_PASSWORD` | 應用程式使用的最小權限帳號 |
| `API_IMAGE` | （僅正式環境）要部署的映像檔，由 CI 帶入 |
| `DOMAIN` | （僅正式環境）Nginx 與憑證使用的網域 |

## 已知限制與規劃

- 目前尚無自動化測試，優先規劃：分錄驗證邏輯、提案確認流程
- 僅支援單一幣別（新台幣）與純文字訊息
- 帳戶為固定清單，不支援使用者自訂
- CI/CD 目前只有建置與部署，尚未在部署前執行測試（待測試補上後加入）