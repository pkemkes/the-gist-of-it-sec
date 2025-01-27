from os import getenv
from flask import Flask
from flask_cors import CORS
from prometheus_flask_exporter import PrometheusMetrics

server_origins = getenv("SERVER_ORIGINS").split(",")

app = Flask(__name__)
metrics = PrometheusMetrics(app)
CORS(app, resources={r"/*": {"origins": server_origins}})

from api import rest_routes