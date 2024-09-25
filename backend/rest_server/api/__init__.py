from os import getenv
from flask import Flask
from flask_cors import CORS

server_origins = getenv("SERVER_ORIGINS").split(",")

app = Flask(__name__)
CORS(app, resources={r"/*": {"origins": server_origins}})

from api import rest_routes