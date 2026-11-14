# Group names are the API's roles: it maps cognito:groups to roles, and its policies require exactly
# "Bidder" or "Admin" (an Admin is not also a Bidder). New accounts join no group by themselves; add them
# with `aws cognito-idp admin-add-user-to-group` (README → "Authenticating with Cognito").
resource "aws_cognito_user_group" "bidder" {
  name         = "Bidder"
  user_pool_id = aws_cognito_user_pool.users.id
  description  = "Can create auctions and place bids."
}

resource "aws_cognito_user_group" "admin" {
  name         = "Admin"
  user_pool_id = aws_cognito_user_pool.users.id
  description  = "Can close auctions and read any user's bid history."
}
