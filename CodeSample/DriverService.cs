using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Diagnostics.Contracts;

namespace Services.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts;
    using Contracts.Create.Response;
    using Contracts.Read.Request;
    using Contracts.Update.Request;
    using Contracts.Update.Response;
    using Data.Local.LocalDatabase;
    using Data.Samsara.Data;
    using Services.CommandServices;
    using Services.Interfaces;
    using Services.QueryServices;
    using Services.QueryServices.Samsara;
    using Services.Validators;
    using Utilities;
    using Utilities.InputValidation;

    public class DriverService : IDriverService
    {

        private readonly ISamsaraDriverQueryService _driverQueryService;
        private readonly IDriverCommandService _driverCommandService;
        private readonly ITextSmsService _textSmsService;
        private readonly ICustomerQueryService _customerQueryService;
        private readonly ICustomerLocalCommandService _customerLocalCommandService;
        private readonly IEmptyStringValidator _emptyStringValidator;
        private readonly IDriverMapper _driverMapper;

        public DriverService(ISamsaraDriverQueryService driverQueryService, IDriverCommandService driverCommandService, ITextSmsService textSmsService,
            IAuthenticationQueryService authenticationQueryService, IEncryptionService encryptionService,
            IDriverAdminQueryService driverAdminQueryService, IDriverAdminCommandService driverAdminCommandService,
            ICustomerQueryService customerQueryService, ICustomerLocalCommandService customerLocalCommandService,
            IEmptyStringValidator emptyStringValidator,
            IDriverMapper driverMapper
            )
        {
            this._driverQueryService = driverQueryService ?? throw new ArgumentNullException(nameof(driverQueryService));
            this._driverCommandService = driverCommandService ?? throw new ArgumentNullException(nameof(driverCommandService));
            this._textSmsService = textSmsService ?? throw new ArgumentNullException(nameof(textSmsService));
            this._customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
            this._customerLocalCommandService = customerLocalCommandService ?? throw new ArgumentNullException(nameof(customerLocalCommandService));
            this._emptyStringValidator = emptyStringValidator ?? throw new ArgumentNullException(nameof(emptyStringValidator));
            this._driverMapper = driverMapper ?? throw new ArgumentNullException(nameof(driverMapper));
        }


        public async Task<DomainResponse<DriverLoginResponseContract>> ValidateUserIdentity(DriverAuthenticationRequestContract driverLoginContract)
        {
            if (driverLoginContract == null)
                throw new ArgumentNullException(nameof(driverLoginContract));

            return await (await ValidateUserName(driverLoginContract.Username)
                .Then(() => ValidatePhoneNumber(driverLoginContract.PhoneNumber))
                .ThenAsync(async driver => await GetActiveDriver(driver)))
                .ThenAsync(async driver => await Authenticate(driver));

        }

        public async Task<DomainResponse<List<DriverContract>>> GetDrivers()
        {

            return (await _driverQueryService.GetDrivers()).ThenAndMap<List<DriverContract>>(
                drivers => new DomainResponse<List<DriverContract>>(_driverMapper.ToContracts(drivers.data)),
             () => new DomainResponse<List<DriverContract>>(ErrorCodes.DriverQueryError));

        }

        private async Task<DomainResponse<DriverLoginResponseContract>> Authenticate(DriverLoginResponseContract contract)
        {

            var driverAuthentication = await _driverCommandService.InvokeCreateAuthenticationCommand(contract);
            if (driverAuthentication.WasUnsuccess())
                return ReturnErrorAndRemoveLocalCustomerData(driverAuthentication.ErrorMessage);

            await _textSmsService.SendTextMessage(string.Format(VerificationMessages.TextMessageVerificationCode, driverAuthentication.SuccessObject.AuthCode), contract.PhoneNumber);
            return new DomainResponse<DriverLoginResponseContract>(contract);
        }



        private async Task<DomainResponse<DriverLoginResponseContract>> GetActiveDriver(DriverLoginResponseContract contract)
        {

            var driversResponse = await _driverQueryService.GetDriverByUserNameAndPhoneNumber(contract.DriverUserName, contract.PhoneNumber);

            if (driversResponse.WasUnsuccess())
                return ReturnErrorAndRemoveLocalCustomerData(driversResponse.ErrorMessage);

            var drivers = driversResponse.SuccessObject.data;
            if (!drivers.Any())
                return ReturnErrorAndRemoveLocalCustomerData(ErrorCodes.DriverNotFoundValidationMessage);

            if (drivers.Count != 1)
                return ReturnErrorAndRemoveLocalCustomerData(ErrorCodes.ErrorCodeMultipleDriversWithSameUserNameAndPhoneNumber);

            var driver = drivers.Single();

            if (driver.IsInactive())
                return ReturnErrorAndRemoveLocalCustomerData(ErrorCodes.DriverNotActiveValidationMessage);

            return new DomainResponse<DriverLoginResponseContract>(_driverMapper.ToLoginContract(driver));

        }


        private DomainResponse<DriverLoginResponseContract> ReturnErrorAndRemoveLocalCustomerData(string error)
        {
            _customerLocalCommandService.Delete();
            return new DomainResponse<DriverLoginResponseContract>(error);

        }

        private DomainResponse<DriverLoginResponseContract> ValidateUserName(string userName)
        {

            return _emptyStringValidator.Validate<DriverLoginResponseContract>(userName, InputValidationMessages.UsernameRequiredValidationMessage);
        }

        private DomainResponse<DriverLoginResponseContract> ValidatePhoneNumber(string phoneNumber)
        {

            return _emptyStringValidator.Validate<DriverLoginResponseContract>(phoneNumber, InputValidationMessages.PhoneNumberRequiredValidationMessage);
        }

    }
}


