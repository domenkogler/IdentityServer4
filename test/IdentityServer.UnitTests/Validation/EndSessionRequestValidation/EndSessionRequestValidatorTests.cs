﻿using FluentAssertions;
using IdentityServer4.Configuration;
using IdentityServer4.Extensions;
using IdentityServer4.Hosting;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UnitTests.Common;
using Xunit;

namespace IdentityServer.UnitTests.Validation.EndSessionRequestValidation
{
    public class EndSessionRequestValidatorTests
    {
        EndSessionRequestValidator _subject;
        IdentityServerContext _context;
        StubTokenValidator _stubTokenValidator = new StubTokenValidator();
        StubRedirectUriValidator _stubRedirectUriValidator = new StubRedirectUriValidator();

        ClaimsPrincipal _user;

        public EndSessionRequestValidatorTests()
        {
            _user = IdentityServer4.IdentityServerPrincipal.Create("alice", "Alice");

            _context = IdentityServerContextHelper.Create();
            _subject = new EndSessionRequestValidator(
                TestLogger.Create<EndSessionRequestValidator>(),
                _context, 
                _stubTokenValidator,
                _stubRedirectUriValidator);
        }

        [Fact]
        public async Task anonymous_user_when_options_require_authenticated_user_should_return_error()
        {
            _context.Options.AuthenticationOptions.RequireAuthenticatedUserForSignOutMessage = true;

            var parameters = new NameValueCollection();
            var result = await _subject.ValidateAsync(parameters, null);
            result.IsError.Should().BeTrue();

            result = await _subject.ValidateAsync(parameters, new ClaimsPrincipal());
            result.IsError.Should().BeTrue();

            result = await _subject.ValidateAsync(parameters, new ClaimsPrincipal(new ClaimsIdentity()));
            result.IsError.Should().BeTrue();
        }

        [Fact]
        public async Task valid_params_should_return_success()
        {
            _stubTokenValidator.IdentityTokenValidationResult = new TokenValidationResult()
            {
                IsError = false,
                Claims = new Claim[] { new Claim("sub", _user.GetSubjectId()) },
                Client = new Client() { ClientId = "client"}
            };
            _stubRedirectUriValidator.IsPostLogoutRedirectUriValid = true;

            var parameters = new NameValueCollection();
            parameters.Add("id_token_hint", "id_token");
            parameters.Add("post_logout_redirect_uri", "http://client/signout-cb");
            parameters.Add("client_id", "client1");
            parameters.Add("state", "foo");

            var result = await _subject.ValidateAsync(parameters, _user);
            result.IsError.Should().BeFalse();

            result.ValidatedRequest.Client.ClientId.Should().Be("client");
            result.ValidatedRequest.PostLogOutUri.Should().Be("http://client/signout-cb");
            result.ValidatedRequest.State.Should().Be("foo");
            result.ValidatedRequest.Subject.GetSubjectId().Should().Be(_user.GetSubjectId());
        }

        [Fact]
        public async Task post_logout_uri_fails_validation_should_return_success()
        {
            _stubTokenValidator.IdentityTokenValidationResult = new TokenValidationResult()
            {
                IsError = false,
                Claims = new Claim[] { new Claim("sub", _user.GetSubjectId()) },
                Client = new Client() { ClientId = "client" }
            };
            _stubRedirectUriValidator.IsPostLogoutRedirectUriValid = false;

            var parameters = new NameValueCollection();
            parameters.Add("id_token_hint", "id_token");
            parameters.Add("post_logout_redirect_uri", "http://client/signout-cb");
            parameters.Add("client_id", "client1");
            parameters.Add("state", "foo");

            var result = await _subject.ValidateAsync(parameters, _user);
            result.IsError.Should().BeTrue();
        }

        [Fact]
        public async Task subject_mismatch_should_return_error()
        {
            _stubTokenValidator.IdentityTokenValidationResult = new TokenValidationResult()
            {
                IsError = false,
                Claims = new Claim[] { new Claim("sub", "xoxo") },
                Client = new Client() { ClientId = "client" }
            };
            _stubRedirectUriValidator.IsPostLogoutRedirectUriValid = true;

            var parameters = new NameValueCollection();
            parameters.Add("id_token_hint", "id_token");
            parameters.Add("post_logout_redirect_uri", "http://client/signout-cb");
            parameters.Add("client_id", "client1");
            parameters.Add("state", "foo");

            var result = await _subject.ValidateAsync(parameters, _user);
            result.IsError.Should().BeTrue();
        }

        [Fact]
        public async Task successful_request_should_return_inputs()
        {
            var parameters = new NameValueCollection();

            var result = await _subject.ValidateAsync(parameters, _user);
            result.IsError.Should().BeFalse();
            result.ValidatedRequest.Raw.Should().BeSameAs(parameters);
            result.ValidatedRequest.Subject.Should().BeSameAs(_user);
        }
    }
}
