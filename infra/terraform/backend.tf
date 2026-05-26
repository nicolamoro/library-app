terraform {
  required_version = ">= 1.6"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  backend "s3" {
    key     = "library-app/terraform.tfstate"
    encrypt = true
    # region, bucket, dynamodb_table are injected at terraform init time via -backend-config flags
    # so they can be sourced from GitHub secrets without being hardcoded here
  }
}
