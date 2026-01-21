# QueryBuilder

## Public Sharing and Embedding

Dashboards can be shared via public, read-only links. Public sharing is opt-in and controlled per dashboard.

### Enable a public dashboard link

1. Open a dashboard and select **Share**.
2. Toggle **Enabled**.
3. (Optional) Set an expiration of 7 or 30 days.
4. Copy the public URL or iframe embed snippet.

### Public link behavior

- Links are read-only and do not expose edit controls.
- Links are tokenized and do not reveal database IDs.
- Disabled or expired links return 404.

### Embed a dashboard

Use the embed snippet provided in the Share dialog. Example:

```html
<iframe src="https://your-host/public/embed/d/{token}" width="100%" height="600" frameborder="0" loading="lazy"></iframe>
```

### Apply database migration

After pulling these changes, apply the migration:

```powershell
dotnet-ef database update -p .\src\QueryBuilder.Web\QueryBuilder.Web.csproj -c QueryBuilder.Web.Data.ApplicationDbContext --no-build
```
