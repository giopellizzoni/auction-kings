resource "aws_security_group" "worker" {
  name        = "${local.name_prefix}-worker"
  description = "Background worker egress only, no inbound HTTP"
  vpc_id      = aws_vpc.this.id

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-worker-sg"
  })
}
