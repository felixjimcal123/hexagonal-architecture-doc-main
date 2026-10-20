using System;
using GtMotive.Estimate.Microservice.ApplicationCore.Exceptions;
using GtMotive.Estimate.Microservice.Domain;
using GtMotive.Estimate.Microservice.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GtMotive.Estimate.Microservice.Api.Filters
{
    public sealed class BusinessExceptionFilter(IAppLogger<BusinessExceptionFilter> appLogger) : IExceptionFilter
    {
        private readonly IAppLogger<BusinessExceptionFilter> _appLogger = appLogger;

        public void OnException(ExceptionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _appLogger.LogError(context.Exception, "Exception captured in BusinessExceptionFilter.");

            if (context.Exception is ConflictException)
            {
                var problem = NewProblem(context, StatusCodes.Status409Conflict, "Conflict");
                _appLogger.LogWarning("Conflict: {status} - {detail}", problem.Status, problem.Detail);
                context.Result = new ConflictObjectResult(problem);
                context.ExceptionHandled = true;
            }
            else if (context.Exception is NotFoundException)
            {
                var problem = NewProblem(context, StatusCodes.Status404NotFound, "Not Found");
                context.Result = new NotFoundObjectResult(problem);
                context.ExceptionHandled = true;
            }
            else if (context.Exception is DomainException)
            {
                var problem = NewProblem(context, StatusCodes.Status400BadRequest, "Bad Request");
                _appLogger.LogWarning("Domain Exception: {status} - {detail}", problem.Status, problem.Detail);
                context.Result = new BadRequestObjectResult(problem);
                context.ExceptionHandled = true;
            }
            else
            {
                var problem = NewProblem(context, StatusCodes.Status500InternalServerError, "Internal Server Error");
                _appLogger.LogError(context.Exception, "Unhandled Exception");
                context.Result = new InternalServerErrorObjectResult(problem);
                context.ExceptionHandled = true;
            }
        }

        private static ProblemDetails NewProblem(ExceptionContext context, int statusCode, string title)
        {
            return new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = statusCode == StatusCodes.Status500InternalServerError ? "An unexpected error occurred." : context.Exception.Message,
                Instance = context.HttpContext.Request.Path,
            };
        }
    }
}
