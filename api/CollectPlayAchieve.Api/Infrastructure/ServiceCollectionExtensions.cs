using FluentValidation;
using Scrutor;

namespace CollectPlayAchieve.Api.Infrastructure;

internal static class ServiceCollectionExtensions
{
    extension(IImplementationTypeSelector selector)
    {
        public IImplementationTypeSelector AddHandlerClasses() =>
            selector
                .AddHandlerClassesAssignableTo(typeof(IQueryHandler<,>))
                .AddHandlerClassesAssignableTo(typeof(ICommandHandler<>))
                .AddHandlerClassesAssignableTo(typeof(ICommandHandler<,>));

        public IImplementationTypeSelector AddValidatorClasses() =>
            selector
                .AddClasses(
                    classes => classes.AssignableTo(typeof(IValidator<>)),
                    publicOnly: false
                )
                .AsImplementedInterfaces()
                .WithScopedLifetime();

        public IImplementationTypeSelector AddEndpointClasses() =>
            selector
                .AddClasses(classes => classes.AssignableTo<IEndpoint>(), publicOnly: false)
                .As<IEndpoint>()
                .WithTransientLifetime();

        private IImplementationTypeSelector AddHandlerClassesAssignableTo(Type assignableTo) =>
            selector
                .AddClasses(
                    classes =>
                        classes
                            .AssignableTo(assignableTo)
                            .Where(type => type.DeclaringType != typeof(ValidationDecorator)),
                    publicOnly: false
                )
                .AsImplementedInterfaces()
                .WithScopedLifetime();
    }

    extension(IServiceCollection services)
    {
        public void AddValidationDecorators()
        {
            services.TryDecorate(
                typeof(ICommandHandler<>),
                typeof(ValidationDecorator.CommandBaseHandler<>)
            );
            services.TryDecorate(
                typeof(ICommandHandler<,>),
                typeof(ValidationDecorator.CommandHandler<,>)
            );
            services.TryDecorate(
                typeof(IQueryHandler<,>),
                typeof(ValidationDecorator.QueryHandler<,>)
            );
        }
    }
}
