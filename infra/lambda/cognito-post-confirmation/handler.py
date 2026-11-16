"""Cognito post-confirmation trigger: puts every newly confirmed account in the default group.

Self-registered users (sign-up + confirm) would otherwise belong to no group, and the API's
policies are role-exact (cognito:groups -> roles), so they could not create auctions or bid.
scripts/cognito-local-signup.sh does the same thing against the local Moto emulator, which does
not run Cognito triggers.
"""

import os

import boto3

_cognito = boto3.client("cognito-idp")
_DEFAULT_GROUP = os.environ["DEFAULT_GROUP"]


def handler(event, context):
    # Also fires as PostConfirmation_ConfirmForgotPassword after a password reset; only new accounts
    # join the group, so a user an admin removed from it stays removed.
    if event.get("triggerSource") == "PostConfirmation_ConfirmSignUp":
        _cognito.admin_add_user_to_group(
            UserPoolId=event["userPoolId"],
            Username=event["userName"],
            GroupName=_DEFAULT_GROUP,
        )

    # Cognito triggers must hand the event back.
    return event
