using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using MyProtWeb.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyProtWeb.webapi
{
    public static class ProtocolEndpoints
    {
        public static void MapProtocolEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/protocols");

            group.MapGet("/", async (IProtocolService svc) =>
            {
                var items = await svc.GetAllAsync();
                return ApiResponse.Ok(items);
            });

            group.MapGet("/{id}", async (string id, IProtocolService svc) =>
            {
                var item = await svc.GetByIdAsync(id);
                return item is not null ? Results.Ok(item) : Results.NotFound();
            });

            group.MapPost("/", async (ProtocolConfig config, IProtocolService svc) =>
            {
                var created = await svc.CreateAsync(config);
                return Results.Created($"/api/protocols/{created.protocolName}", created);
            });

            group.MapPut("/{id}", async (string id, ProtocolConfig config, IProtocolService svc) =>
            {
                var updated = await svc.UpdateAsync(id, config);
                return updated ? Results.NoContent() : Results.NotFound();
            });

            group.MapDelete("/{id}", async (string id, IProtocolService svc) =>
            {
                var deleted = await svc.DeleteAsync(id);
                return deleted ? Results.NoContent() : Results.NotFound();
            });
        }
    }
}
