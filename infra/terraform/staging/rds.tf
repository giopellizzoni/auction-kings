resource "random_password" "db" {
  length           = 32
  special          = true
  override_special = "!#$%&*()-_=+[]<>?"
}

resource "aws_db_subnet_group" "postgres" {
  name       = "${local.name_prefix}"
  subnet_ids = aws_subnet.private[*].id

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-db-subnet-group"
  })
}

resource "aws_db_instance" "postgres" {
  identifier = "${local.name_prefix}"

  engine         = "postgres"
  engine_version = "16"
  instance_class = "db.t4g.micro"

  db_name  = var.rds.name
  username = var.rds.username
  password = random_password.db.result

  db_subnet_group_name   = aws_db_subnet_group.postgres.name
  vpc_security_group_ids = [aws_security_group.rds.id]

  # Staging: single-AZ, no Multi-AZ standby, minimal storage
  multi_az              = false
  allocated_storage     = 20
  max_allocated_storage = 100
  storage_type          = "gp3"
  storage_encrypted     = true

  backup_retention_period = 3
  deletion_protection     = false
  skip_final_snapshot     = true

  # Apply updates immediately in staging
  auto_minor_version_upgrade = true
  apply_immediately          = true

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-postgres"
  })
}
