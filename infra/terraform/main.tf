provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project = "library-app"
    }
  }
}

data "aws_caller_identity" "current" {}

data "aws_ami" "amazon_linux_2023" {
  most_recent = true
  owners      = ["amazon"]

  filter {
    name   = "name"
    values = ["al2023-ami-2023.*-x86_64"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

resource "aws_security_group" "app" {
  name        = "library-app-sg"
  description = "Library App: allow HTTP 8080 from internet; no SSH (SSM used)"

  ingress {
    description = "App HTTP"
    from_port   = 8080
    to_port     = 8080
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    description = "Outbound (SSM endpoints, ECR, mcr.microsoft.com, S3)"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_instance" "app" {
  ami                    = data.aws_ami.amazon_linux_2023.id
  instance_type          = var.instance_type
  iam_instance_profile   = aws_iam_instance_profile.ec2.name
  vpc_security_group_ids = [aws_security_group.app.id]

  root_block_device {
    volume_type = "gp2"
    volume_size = 20
    encrypted   = true
  }

  user_data = file("${path.module}/user_data.sh")

  tags = {
    Name = "library-app"
  }
}

resource "aws_eip" "app" {
  instance = aws_instance.app.id
  domain   = "vpc"
}
