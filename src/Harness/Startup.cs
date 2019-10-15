using GraphQL.Authorization;
using GraphQL.Server;
using GraphQL.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Linq;
using System.Security.Claims;

namespace Harness
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // first registration wins
            services.TryAddSingleton<ISchema, MySchema>();
            services.TryAddSingleton<ISchema>(s =>
            {
                var definitions = @"
                  type User {
                    id: ID
                    name: String
                  }

                  type Query {
                    viewer: User
                    users: [User]
                  }
                ";
                var schema = Schema.For(
                    definitions,
                    builder =>
                    {
                        builder.Types.Include<Query>();
                    });
                schema.FindType("User").AuthorizeWith("AdminPolicy");
                return schema;
            });

            // extension method defined in this project
            services.AddGraphQLAuth(settings =>
            {
                settings.AddPolicy("AdminPolicy", builder => builder.RequireClaim("role", "Admin"));
            });

            services.AddGraphQL(options =>
            {
                options.ExposeExceptions = true;
                options.EnableMetrics = false;
            }).AddUserContextBuilder(context =>
            {
                var c = new GraphQLUserContext { User = context.User };
                // just for example override user with test value claims
                c.User = new ClaimsPrincipal(new ClaimsIdentity[] { new ClaimsIdentity(new Claim[] { new Claim("a", "1"), new Claim("b", "2") }, "blabla") }); 
                return c;
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseDeveloperExceptionPage()
               .UseGraphQL<ISchema>()
               .UseGraphiQLServer();
        }
    }

    public class MySchema : Schema
    {
        public MySchema(IServiceProvider p) : base(p)
        {
            Query = new MyQuery();
        }
    }

    public class MyQuery : ObjectGraphType
    {
        public MyQuery()
        {
            Field<StringGraphType>("test", resolve: context =>
                {
                    var provider = (IProvideClaimsPrincipal)context.UserContext;
                    return string.Join(", ", provider.User.Claims.Select(c => $"{c.Type}={c.Value}"));
                });
        }
    }
}
