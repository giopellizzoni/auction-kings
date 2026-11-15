# Group names are the API's roles: it maps cognito:groups to roles, and its policies require exactly
# "Bidder" or "Admin" (an Admin is not also a Bidder). Every newly confirmed account joins Bidder through
# the post-confirmation trigger (lambda.cognito-post-confirmation.tf); Admin is granted by hand.
resource "aws_cognito_user_group" "bidder" {
  name         = local.default_user_group
  user_pool_id = aws_cognito_user_pool.users.id
  description  = "Can create auctions and place bids."
}

resource "aws_cognito_user_group" "admin" {
  name         = "Admin"
  user_pool_id = aws_cognito_user_pool.users.id
  description  = "Can close auctions and read any user's bid history."
}
