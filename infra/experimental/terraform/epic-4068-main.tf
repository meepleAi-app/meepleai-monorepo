# Epic #4068: Infrastructure as Code (Terraform)

terraform {
  required_version = ">= 1.6"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    postgresql = {
      source  = "cyrilgdn/postgresql"
      version = "~> 1.21"
    }
  }

  backend "s3" {
    bucket = "meepleai-terraform-state"
    key    = "epic-4068/terraform.tfstate"
    region = "us-east-1"
  }
}

provider "aws" {
  region = var.aws_region
}

provider "postgresql" {
  host     = var.db_host
  port     = 5432
  database = "meepleai"
  username = var.db_admin_user
  password = var.db_admin_password
  sslmode  = "require"
}

# Variables
variable "aws_region" {
  description = "AWS region for deployment"
  type        = string
  default     = "us-east-1"
}

variable "environment" {
  description = "Environment (dev, staging, production)"
  type        = string
}

variable "db_host" {
  description = "PostgreSQL host"
  type        = string
}

variable "db_admin_user" {
  description = "PostgreSQL admin user"
  type        = string
  sensitive   = true
}

variable "db_admin_password" {
  description = "PostgreSQL admin password"
  type        = string
  sensitive   = true
}

# RDS Instance for Permission System
resource "aws_db_instance" "meepleai_db" {
  identifier = "meepleai-${var.environment}"
  engine     = "postgres"
  engine_version = "16.1"

  instance_class    = var.environment == "production" ? "db.t3.medium" : "db.t3.micro"
  allocated_storage = var.environment == "production" ? 100 : 20

  db_name  = "meepleai"
  username = var.db_admin_user
  password = var.db_admin_password

  # Epic #4068: Performance tuning for permission queries
  parameter_group_name = aws_db_parameter_group.permission_optimized.name

  # Backup configuration
  backup_retention_period = 7
  backup_window          = "03:00-04:00"
  maintenance_window     = "mon:04:00-mon:05:00"

  # Security
  storage_encrypted = true
  publicly_accessible = false
  vpc_security_group_ids = [aws_security_group.db_sg.id]

  # Monitoring
  enabled_cloudwatch_logs_exports = ["postgresql", "upgrade"]
  monitoring_interval             = 60
  monitoring_role_arn            = aws_iam_role.rds_monitoring.arn

  tags = {
    Name        = "MeepleAI Database"
    Environment = var.environment
    Epic        = "4068"
    ManagedBy   = "Terraform"
  }
}

# Parameter Group: Optimized for Permission Queries
resource "aws_db_parameter_group" "permission_optimized" {
  name   = "meepleai-permission-optimized-${var.environment}"
  family = "postgres16"

  # Epic #4068: Index optimization
  parameter {
    name  = "random_page_cost"
    value = "1.1" # SSD-optimized (favor index scans)
  }

  parameter {
    name  = "effective_cache_size"
    value = "4GB" # Larger cache for index data
  }

  parameter {
    name  = "shared_buffers"
    value = "1GB" # Cache frequently accessed rows (User permissions)
  }

  parameter {
    name  = "work_mem"
    value = "16MB" # Sufficient for permission queries
  }

  # Connection pooling
  parameter {
    name  = "max_connections"
    value = "200"
  }

  # Logging for slow queries
  parameter {
    name  = "log_min_duration_statement"
    value = "100" # Log queries > 100ms
  }

  parameter {
    name  = "log_statement"
    value = "mod" # Log all data-modifying statements
  }

  tags = {
    Epic = "4068"
  }
}

# Security Group: Database Access
resource "aws_security_group" "db_sg" {
  name        = "meepleai-db-${var.environment}"
  description = "Security group for MeepleAI PostgreSQL database"
  vpc_id      = var.vpc_id

  # Inbound: PostgreSQL from application servers only
  ingress {
    description     = "PostgreSQL from API servers"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.api_sg.id]
  }

  # Outbound: Deny all (database doesn't initiate connections)
  egress {
    description = "Deny all outbound"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = []
  }

  tags = {
    Name = "MeepleAI DB Security Group"
    Epic = "4068"
  }
}

# Redis for Permission Caching (Future Enhancement)
resource "aws_elasticache_cluster" "permission_cache" {
  cluster_id           = "meepleai-permission-cache-${var.environment}"
  engine               = "redis"
  engine_version       = "7.1"
  node_type            = "cache.t3.micro"
  num_cache_nodes      = 1
  parameter_group_name = aws_elasticache_parameter_group.redis_params.name
  port                 = 6379
  security_group_ids   = [aws_security_group.redis_sg.id]

  tags = {
    Name        = "Permission Cache"
    Environment = var.environment
    Epic        = "4068"
  }
}

resource "aws_elasticache_parameter_group" "redis_params" {
  name   = "meepleai-redis-params-${var.environment}"
  family = "redis7"

  # Permission cache: 5-minute TTL
  parameter {
    name  = "maxmemory-policy"
    value = "allkeys-lru" # Evict least recently used
  }

  parameter {
    name  = "timeout"
    value = "300" # 5-minute timeout (matches permission cache TTL)
  }
}

# CloudWatch Alarms for Epic #4068
resource "aws_cloudwatch_metric_alarm" "permission_api_latency" {
  alarm_name          = "meepleai-permission-api-latency-${var.environment}"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 2
  metric_name         = "Duration"
  namespace           = "AWS/ApplicationELB"
  period              = 300
  statistic           = "Average"
  threshold           = 100 # 100ms p95
  alarm_description   = "Permission API latency > 100ms (Epic #4068)"
  alarm_actions       = [aws_sns_topic.alerts.arn]

  dimensions = {
    TargetGroup  = aws_lb_target_group.api.arn_suffix
    LoadBalancer = aws_lb.main.arn_suffix
  }

  tags = {
    Epic = "4068"
  }
}

resource "aws_cloudwatch_metric_alarm" "permission_error_rate" {
  alarm_name          = "meepleai-permission-error-rate-${var.environment}"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 1
  metric_name         = "HTTPCode_Target_5XX_Count"
  namespace           = "AWS/ApplicationELB"
  period              = 300
  statistic           = "Sum"
  threshold           = 10 # > 10 errors in 5 minutes
  alarm_description   = "Permission API error rate high (Epic #4068)"
  alarm_actions       = [aws_sns_topic.alerts.arn]

  dimensions = {
    TargetGroup  = aws_lb_target_group.api.arn_suffix
    LoadBalancer = aws_lb.main.arn_suffix
  }
}

# S3 Bucket for Database Backups
resource "aws_s3_bucket" "db_backups" {
  bucket = "meepleai-db-backups-${var.environment}"

  tags = {
    Name        = "Database Backups"
    Environment = var.environment
    Epic        = "4068"
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "db_backups_lifecycle" {
  bucket = aws_s3_bucket.db_backups.id

  rule {
    id     = "delete-old-backups"
    status = "Enabled"

    # Keep backups for 30 days
    expiration {
      days = 30
    }

    # Transition to cheaper storage after 7 days
    transition {
      days          = 7
      storage_class = "STANDARD_IA"
    }
  }
}

# IAM Role for RDS Monitoring
resource "aws_iam_role" "rds_monitoring" {
  name = "meepleai-rds-monitoring-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "monitoring.rds.amazonaws.com"
      }
    }]
  })

  managed_policy_arns = [
    "arn:aws:iam::aws:policy/service-role/AmazonRDSEnhancedMonitoringRole"
  ]

  tags = {
    Epic = "4068"
  }
}

# Database User for Application
resource "postgresql_role" "app_user" {
  name     = "meepleai_app"
  login    = true
  password = var.db_app_password

  # Limited permissions (not superuser)
  superuser = false
  createdb  = false
  createrole = false

  # Connection limit
  connection_limit = 100
}

# Grant permissions on Users table
resource "postgresql_grant" "app_user_users_table" {
  database    = "meepleai"
  role        = postgresql_role.app_user.name
  object_type = "table"
  objects     = ["Users"]

  privileges = ["SELECT", "INSERT", "UPDATE", "DELETE"]
}

# Grant usage on schema
resource "postgresql_grant" "app_user_schema" {
  database    = "meepleai"
  role        = postgresql_role.app_user.name
  object_type = "schema"
  schema      = "public"

  privileges = ["USAGE"]
}

# Outputs
output "db_endpoint" {
  description = "Database endpoint for connection"
  value       = aws_db_instance.meepleai_db.endpoint
  sensitive   = true
}

output "redis_endpoint" {
  description = "Redis endpoint for caching"
  value       = aws_elasticache_cluster.permission_cache.cache_nodes[0].address
}

output "backup_bucket" {
  description = "S3 bucket for database backups"
  value       = aws_s3_bucket.db_backups.bucket
}
