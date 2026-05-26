variable "aws_region" {
  description = "AWS region to deploy into"
  type        = string
  default     = "us-east-1"
}

variable "instance_type" {
  description = "EC2 instance type (t2.micro is in the free tier)"
  type        = string
  default     = "t2.micro"
}

variable "backend_bucket" {
  description = "S3 bucket used for Terraform state; also stores db-init scripts"
  type        = string
}
