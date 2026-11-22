# Meeting Room Booking

## About

Guests can browse and filter meeting rooms, book one using just their name, and view, edit or cancel their bookings from the same browser — no account needed.

## Run locally

Prerequisites: .NET SDK 10.0.404 or later 10.0, Node.js 22.22.3+ or 24.15+, and npm.

Terminal 1 — API on http://localhost:5050. The SQLite database and seed rooms are created automatically on first start.

```powershell
cd backend
dotnet run --project MeetingBooking.Api --launch-profile http
```

Terminal 2 — Angular on http://localhost:4200, which proxies `/api` to the API. Run `npm install` the first time only.

```powershell
cd frontend
npm install
npm start
```

Open http://localhost:4200.
