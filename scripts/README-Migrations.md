# Migrations and Database Initialization

Create the initial migration and apply it to the database.

From the solution directory run:

1. Add migration

```
dotnet ef migrations add InitialCreate --project HMS.API --startup-project HMS.API
```

2. Apply migrations

```
dotnet ef database update --project HMS.API --startup-project HMS.API
```

If using Visual Studio Package Manager Console, set Default project to `HMS.API` and run:

```
Add-Migration InitialCreate
Update-Database
```

Configure the connection string in `appsettings.json` or environment variable `ConnectionStrings__Default`.
