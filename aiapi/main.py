import argparse
from os import getenv
import uvicorn

from api import app


def main() -> None:
    print(getenv("OPENAI_API_KEY"))

    parser = argparse.ArgumentParser(description="Summarizer API utilities")
    parser.add_argument("--host", default="0.0.0.0", help="Host for the dev server")
    parser.add_argument("--port", type=int, default=8000, help="Port for the dev server")
    parser.add_argument(
        "--reload",
        action="store_true",
        help="Enable auto-reload when running the dev server",
    )
    args = parser.parse_args()

    uvicorn.run(
        "api:app",
        host=args.host,
        port=args.port,
        reload=args.reload,
    )


if __name__ == "__main__":
    main()
