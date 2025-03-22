# Mesajlaşma Projesi

Overview

This project is a real-time messaging platform that allows users to communicate with each other instantly. The system supports user registration, login, real-time chat using WebSockets, and message history management. It utilizes modern technologies like React.js, .NET, Firebase, and Docker to ensure security, scalability, and a great user experience.

Key Features

Real-Time Messaging: Instant communication between users via WebSocket (SignalR).

User Management: Secure registration and login with email and password, managed via Firebase Authentication.

Message History: Messages are stored in Firebase Firestore, allowing users to view past conversations in real time.

Group and Direct Messaging: Users can send direct messages or create groups for multiple participants.

Mobile-Friendly: The platform is designed to be responsive and optimized for mobile devices.

Technology Stack

Frontend:
React.js: JavaScript library for building the user interface.
Tailwind CSS: Utility-first CSS framework for building responsive layouts.
WebSockets (Socket.io): For real-time communication between users.

Backend:

.NET (ASP.NET Core): The backend API developed using .NET for handling requests, messaging logic, and user authentication.

Firebase: Firebase Authentication for secure login and user management.

SignalR: For WebSocket-based real-time messaging.

Firebase Firestore: A NoSQL cloud database to store user data, messages, and conversation history in a scalable and real-time manner.

Containerization & Deployment:

Docker: To containerize the application for seamless deployment across different environments.

Firebase Hosting: For hosting the frontend.

CI/CD: GitHub Actions (or Jenkins) for continuous integration and deployment automation.

Project Setup

Prerequisites

Node.js (for frontend): Install Node.js

.NET SDK (for backend): Download .NET SDK

Docker: Install Docker

Firebase Firestore: Set up Firebase Firestore for real-time data storage and retrieval.

Firebase account: For user authentication and real-time messaging.

CI/CD Pipeline

The project uses a CI/CD pipeline to automate builds and deployments:

Continuous Integration (CI):

Every commit triggers a build and runs unit tests to ensure code quality and integrity.

GitHub Actions (or Jenkins) are used for CI.

Continuous Deployment (CD):

Once the code passes all tests, it is automatically deployed to the production environment using Docker.

The production environment is hosted on Heroku, AWS, or similar platforms.

Firebase Hosting and Cloud Functions handle the deployment of the frontend and backend for real-time messaging.
