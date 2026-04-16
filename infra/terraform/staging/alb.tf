resource "aws_lb" "api" {
  name               = "auction-kings-staging"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = aws_subnet.public[*].id

  # Staging: access logs optional (costs S3); enable for prod
  enable_deletion_protection = false

  tags = { Name = "auction-kings-staging-alb" }
}

resource "aws_lb_target_group" "api" {
  name        = "auction-kings-staging-api"
  port        = 8080
  protocol    = "HTTP"
  vpc_id      = aws_vpc.main.id
  target_type = "ip"   # Required for Fargate

  health_check {
    path                = "/health/live"
    interval            = 30
    timeout             = 10
    healthy_threshold   = 2
    unhealthy_threshold = 3
    matcher             = "200"
  }

  deregistration_delay = 30

  tags = { Name = "auction-kings-staging-api-tg" }
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.api.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api.arn
  }
}
