# Stockroom frontend

React and Tailwind dashboard for the Stockroom ASP.NET Core API.

## Run locally

1. Start the API using its `http` profile (`http://localhost:5008`).
2. From this folder, run `npm install` once, then `npm run dev`.
3. Open the Vite URL shown in the terminal (normally `http://localhost:5173`).

The Vite development server proxies `/api` requests to `http://localhost:5008`. Set `API_PROXY_TARGET` to change the backend address. For example, use `https://localhost:7077` if running the API's HTTPS profile.

Sign in with an account provisioned by the API. Public registration is available from the sign-in screen; the backend assigns new accounts the Viewer role. Admin and InventoryManager accounts can create and edit products or adjust stock. Only Admin accounts can delete products and manage team roles.

## Included workflows

- JWT login and account registration, with the token kept in browser local storage.
- Inventory summary, catalog search and pagination, and low-stock report.
- Product creation and editing, stock receipt and removal, and movement history.
- Admin user role management and API-enforced product deletion.
- Responsive desktop and mobile layouts.
