# ChitChatProject

This project is a full-stack, enterprise-ready, real-time messaging platform. It allows users to communicate instantly through a secure, scalable, and responsive interface.

The architecture is built on a modern stack, including React.js for the frontend, .NET (ASP.NET Core) for the backend API, SignalR for real-time communication, and MySQL for persistent data storage.

## Key Features

* **Real-Time Messaging:** Instant, low-latency communication between users powered by **ASP.NET SignalR (WebSockets)**.
* **Secure User Management:** Secure, token-based registration and login using **JSON Web Tokens (JWT)**. All user routes are protected and require valid authorization.
* **Persistent Message History:** All message history, user data, and group information are stored persistently in a **MySQL** relational database.
* **Group and Direct Messaging:** Supports both one-to-one direct messages and multi-participant group chats.
* **Mobile-Friendly:** A fully responsive user interface built with **Tailwind CSS** that works seamlessly on both desktop and mobile devices.
* **Advanced Modules:** Includes architecture for file sharing, digital signatures, calendar/note synchronization, and advanced user settings.

## Technology Stack

### Frontend
* **React.js**: A JavaScript library for building the dynamic user interface.
* **Tailwind CSS**: A utility-first CSS framework for modern, responsive design.
* **SignalR Client (JavaScript)**: The client-side library to establish and manage the persistent WebSocket connection with the backend.

### Backend
* **.NET (ASP.NET Core)**: The backend API framework used for handling all business logic, data processing, and serving API endpoints.
* **SignalR**: An ASP.NET Core library for WebSocket-based, real-time, bi-directional communication.
* **JWT (JSON Web Tokens)**: Used for creating and validating secure, stateless access tokens for user authentication and authorization.

### Database
* **MySQL**: A robust, open-source relational database (SQL) used to store all application data, including users, messages, groups, and relationships.

### DevOps & Deployment
* **Docker**: The entire application (frontend and backend) is containerized for consistent environments and scalable deployment.
* **CI/CD**: Configured for a Continuous Integration/Continuous Deployment pipeline using **GitHub Actions** (or Jenkins) to automate testing and builds.
* **Hosting**: The containerized application is designed to be deployed on any modern cloud platform such as **Heroku**, **AWS**, **Azure**, or any server supporting Docker.

## Project Setup

### Prerequisites

* [Node.js](https://nodejs.org/en/) (for frontend development)
* [.NET SDK](https://dotnet.microsoft.com/download) (for backend development)
* [Docker](https://www.docker.com/products/docker-desktop) (for running the application in containers)
* A running **MySQL Server** instance.

### Installation

1.  **Clone the repository:**
    ```bash
    git clone [https://github.com/et1613/chitchatproject.git](https://github.com/et1613/chitchatproject.git)
    cd chitchatproject
    ```

2.  **Configure Backend (.NET):**
    * Navigate to the backend project directory.
    * Update `appsettings.json` with your **MySQL database connection string** and your **JWT Secret Key**.
    * Run the database migrations:
        ```bash
        dotnet ef database update
        ```
    * Run the backend server:
        ```bash
        dotnet run
        ```

3.  **Configure Frontend (React):**
    * Navigate to the frontend project directory.
    * Install dependencies:
        ```bash
        npm install
        ```
    * Start the development server:
        ```bash
        npm start
        ```

4.  **Running with Docker (Recommended):**
    * Ensure your `docker-compose.yml` file has the correct environment variables (especially for the MySQL connection).
    * Build and run the containers:
        ```bash
        docker-compose up --build
        ```
