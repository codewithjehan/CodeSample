
namespace Utilities
{
    using System;
    using System.Threading.Tasks;

    public class DomainResponse<T> where T : class
    {

        public bool WasSuccess() => string.IsNullOrWhiteSpace(ErrorMessage) && SuccessObject != null;
        public bool WasUnsuccess() => !WasSuccess();
        public string ErrorMessage { get; }
        public T SuccessObject { get; }


        public DomainResponse(T successObject)
        {

            SuccessObject = successObject ?? throw new ArgumentNullException(nameof(successObject));
        }

        public DomainResponse(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException($"'{nameof(errorMessage)}' cannot be null or whitespace.", nameof(errorMessage));
            }

            ErrorMessage = errorMessage;
        }


        public DomainResponse<T> Then(Func<DomainResponse<T>> nextCall)
        {
            if (WasUnsuccess())
                return this;

            return nextCall();
        }

        public async Task<DomainResponse<T>> ThenAsync(Func<T, Task<DomainResponse<T>>> nextCall)
        {
            if (WasUnsuccess())
                return this;

            return await nextCall(SuccessObject);

        }

        public DomainResponse<TMap> ThenAndMap<TMap>(Func<T, DomainResponse<TMap>> nextCall, Func<DomainResponse<TMap>> errorCall) where TMap : class
        {
            if (WasUnsuccess())
                return errorCall();

            return nextCall(SuccessObject);

        }

    }
}

