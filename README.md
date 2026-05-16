# Leafy Library

A library management system built with ASP.NET Core Blazor Server and MongoDB. Leafy Library demonstrates intelligent data application patterns using MongoDB Atlas Search, vector embeddings, and advanced aggregation pipelines.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![MongoDB](https://img.shields.io/badge/MongoDB-7.0+-47A248)
![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4)

---

## Features

- Browse and search a book catalogue with full-text Atlas Search
- Borrow and return books with inventory management
- Reserve books with automatic 12-hour TTL expiration
- Write and view reviews with star ratings
- User dashboard with loan statistics and reading history
- Admin panel for managing books, loans, and reservations
- Semantic search via vector embeddings (OpenAI / Azure OpenAI / VoyageAI)
- JWT authentication with automatic admin promotion for the first user

---

## Requirements

| Requirement | Version |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ |
| [MongoDB](https://www.mongodb.com/try/download/community) | 7.0+ (local) or [Atlas](https://www.mongodb.com/atlas) |
| Git | Any recent version |


**For vector/semantic search (optional):** An API key for one of the following embedding providers:
- [OpenAI](https://platform.openai.com/)
- [Azure OpenAI](https://azure.microsoft.com/products/ai-services/openai-service)
- [VoyageAI](https://www.voyageai.com/)

---

## Getting Started

### 1. Clone the repository

```bash
git clone <repo-url>
cd "Leafy Library"
```

### 2. Configure the application

Copy or edit `appsettings.json` with your settings. At a minimum, update the MongoDB connection string and JWT secret:

```json
{
  "MongoDb": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "leafy_library"
  },
  "Jwt": {
    "Secret": "your-secure-secret-key-at-least-32-characters-long",
    "ExpiryInDays": 365,
    "Issuer": "LeafyLibrary",
    "Audience": "LeafyLibraryUsers"
  }
}
```

For an Atlas cluster, replace the connection string with your Atlas URI:

```
mongodb+srv://<username>:<password>@<cluster>.mongodb.net/?retryWrites=true&w=majority
```

> **Important:** Never commit your real JWT secret or Atlas credentials. Use `appsettings.Development.json` or environment variables for local secrets.

### 3. Run the application

```bash
dotnet run --project "Leafy Library"
```

The app starts at `https://localhost:5001` by default. The first time you log in, your account is automatically promoted to admin.

### 5. Load sample data (optional)

The app starts with an empty database. You can populate it by importing data via the Admin panel once logged in, or by loading a MongoDB dump directly into the `leafy_library` database.

---

## Project Structure

```
Leafy Library/
├── Controllers/          # REST API endpoints
│   ├── BooksController
│   ├── AuthorsController
│   ├── ReviewsController
│   ├── IssueDetailsController
│   └── ReservationsController
├── Services/             # Business logic
│   ├── DatabaseService         # MongoDB connection and typed collections
│   ├── BookService             # Book CRUD and Atlas Search
│   ├── AuthorService           # Author queries and relationships
│   ├── ReviewService           # Reviews with subset pattern sync
│   ├── UserService             # User creation and lookup
│   ├── TokenService            # JWT generation and validation
│   ├── IssueDetailService      # Loan and reservation logic + aggregations
│   ├── ReservationService      # Book reservation management
│   └── EmbeddingService        # Vector embedding generation
├── Models/               # BSON-mapped data models
│   ├── Book
│   ├── Author
│   ├── Review
│   ├── User
│   ├── IssueDetail
│   └── (DTOs and settings models)
├── Components/           # Blazor UI
│   ├── Pages/
│   │   ├── Home.razor          # Book catalogue with search
│   │   ├── BookDetail.razor    # Book info, reviews, borrow/reserve
│   │   ├── AuthorDetail.razor  # Author bio and books
│   │   ├── Login.razor         # Login and auto-registration
│   │   ├── Dashboard.razor     # Loan stats and reading history
│   │   └── Admin.razor         # Admin management panel
│   ├── Layout/                 # App shell and navigation
│   └── Shared/                 # Reusable components (StarRating, ReviewForm, etc.)
├── Program.cs            # Dependency injection and middleware setup
├── appsettings.json      # Application configuration
└── Leafy Library.csproj  # Project file
```

---

## Architecture

### Stack

| Layer | Technology |
|---|---|
| Frontend | Blazor Server (interactive SSR) |
| Backend | ASP.NET Core 10 REST API |
| Database | MongoDB |
| Auth | JWT (stored in ProtectedBrowserStorage) |
| Search | MongoDB Atlas Search ($search) |
| Semantic Search | Vector embeddings via $vectorSearch |

### Data Collections

| Collection | Description |
|---|---|
| `books` | Book catalogue with embedded reviews (subset pattern) and optional embedding vector |
| `authors` | Author profiles with references to books |
| `reviews` | Full review documents (also partially embedded in books) |
| `users` | User accounts with admin flag |
| `issueDetails` | Unified collection for loans and reservations (single-collection pattern) |

### MongoDB Patterns

**Extended Reference Pattern** — Book documents embed author names alongside the author ID to avoid lookups for display.

**Subset Pattern** — Each book stores the 5 most recent reviews inline. The full review history lives in the `reviews` collection.

**Single-Collection Pattern** — Loans and reservations share the `issueDetails` collection, distinguished by a type field and composite ID format.

**TTL Index** — Reservations automatically expire after 12 hours via a MongoDB TTL index on `expirationDate`.

**Atomic Inventory** — `$inc` operations handle concurrent borrow and return operations safely.

### Search

Full-text search is powered by a MongoDB Search index named `fulltextsearch`. The app creates this index automatically on startup if it does not exist. It covers:
- Book title
- Author names
- Genres

Vector search uses the `embedding` field on book documents. Embeddings are generated from the book synopsis and stored at indexing time.

### Authentication Flow

1. User submits a username on the Login page.
2. A `GET /api/users/login/{username}` request gets or creates the user.
3. A signed JWT is returned and stored in browser protected storage.
4. Subsequent requests include the JWT as a Bearer token.
5. The first user account created is automatically promoted to admin.

---

## API Reference

### Books

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/books` | — | List books (paginated) |
| GET | `/api/books/{id}` | — | Get book by ID |
| GET | `/api/books/search?q=` | — | Full-text search |
| POST | `/api/books` | Required | Create book |
| PUT | `/api/books/{id}` | Required | Update book |
| DELETE | `/api/books/{id}` | Required | Delete book |

### Authors

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/authors` | — | List authors (paginated) |
| GET | `/api/authors/{id}` | — | Get author |
| GET | `/api/authors/name/{name}` | — | Get by sanitized name |
| GET | `/api/authors/search?q=` | — | Search authors |

### Reviews

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/books/{bookId}/reviews` | — | List reviews for a book |
| POST | `/api/books/{bookId}/reviews` | Required | Add a review |
| DELETE | `/api/books/{bookId}/reviews/{id}` | Required | Delete a review |

### Loans

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/issuedetails/user/{userId}/active` | Required | Active borrows |
| POST | `/api/issuedetails/borrow` | Required | Borrow a book |
| POST | `/api/issuedetails/return/{issueId}` | Required | Return a book |
| POST | `/api/issuedetails/borrow/{bookId}/{userId}` | Admin | Admin lend a book |

### Reservations

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/reservations` | Required | List user's reservations |
| POST | `/api/reservations/{bookId}` | Required | Reserve a book |
| DELETE | `/api/reservations/{bookId}` | Required | Cancel a reservation |

### Authentication

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/users/login/{username}` | Login or register, returns JWT |

---

## Branches

This repo is structured around an incremental tutorial series:

| Branch | Description |
|---|---|
| `main` | Production-ready baseline |
| `start` | Starting point — minimal implementation |
| `with-text-search` | Search full-text search added |
| `with-vector-search` | Vector embeddings and semantic search added |

---

## Configuration Reference

| Key | Default | Description |
|---|---|---|
| `MongoDb:ConnectionString` | `mongodb://localhost:27017` | MongoDB connection URI |
| `MongoDb:DatabaseName` | `leafy_library` | Database name |
| `Jwt:Secret` | *(must change)* | Signing key, minimum 32 characters |
| `Jwt:ExpiryInDays` | `365` | Token lifetime in days |
| `Jwt:Issuer` | `LeafyLibrary` | JWT issuer claim |
| `Jwt:Audience` | `LeafyLibraryUsers` | JWT audience claim |
| `Embedding:Provider` | *(optional)* | `OpenAI`, `AzureOpenAI`, or `VoyageAI` |
| `Embedding:ApiKey` | *(optional)* | API key for the embedding provider |
| `Embedding:Model` | *(optional)* | Embedding model name |
| `Embedding:Dimensions` | `1536` | Vector dimensions |
| `Embedding:Endpoint` | *(optional)* | Azure OpenAI endpoint URL |
