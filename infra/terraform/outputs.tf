output "elastic_ip" {
  description = "Public IP of the EC2 instance (set this as EC2_HOST for reference)"
  value       = aws_eip.app.public_ip
}

output "instance_id" {
  description = "EC2 instance ID — save this as the EC2_INSTANCE_ID GitHub secret"
  value       = aws_instance.app.id
}

output "ecr_registry" {
  description = "ECR registry hostname (account.dkr.ecr.region.amazonaws.com)"
  value       = "${data.aws_caller_identity.current.account_id}.dkr.ecr.${var.aws_region}.amazonaws.com"
}
