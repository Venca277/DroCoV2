#!/usr/bin/env python3

import sys, getopt
import json
import os
import websocket
import _thread
import time
import subprocess
import shlex

filename = ""
server = ""
port = ""
drone_type = ""
drone_serial = ""

def on_message(ws, message):
    msg = json.loads(message)
    client_id = msg["data"]["client_id"]
    if msg["type"] == "hello_resp":
        def run(*args):
            file = open(filename, "r")
            for line in file:
                j_line = json.loads(line)
                j_line["data"]["client_id"] = client_id
                #adjusted_line = json.dumps({"type":"data_broadcast", "data":j_line})
                ws.send(json.dumps(j_line))
                time.sleep(0.05)
            time.sleep(1)
            ws.close()
            print("thread terminating...")

        _thread.start_new_thread(run, ())

def on_error(ws, error):
    print(error)

def on_close(ws):
    print("### closed ###")

def on_open(ws):
    ws.send(json.dumps({"type":"hello","data":{"ctype":0, "drone_name":drone_type, "serial":drone_serial}}))



def main(argv):
    global filename, drone_type, drone_serial
    try:
        opts, args = getopt.getopt(argv, "hf:s:p:t:d:", ["file=", "server=", "port=", "drone_type=", "drone_serial="])
    except getopt.GetoptError:
        print("client.py -f <filename> -s <server> -p <port> -t <drone_type> -d <drone_serial>")
    for opt, arg in opts:
        if opt == "-h":
            print("client.py -f <filename> -s <server> -p <port> -t <drone_type> -d <drone_serial>")
            sys.exit()
        elif opt in ("-f", "--file"):
            filename = arg
        elif opt in ("-s", "--server"):
            server = arg
        elif opt in ("-p", "--port"):
            port = arg
        elif opt in ("-t", "--drone_type"):
            drone_type = arg
        elif opt in ("-d", "--drone_serial"):
            drone_serial = arg
    if filename == "" or server == "" or port == "" or drone_type == "" or drone_serial == "":
        print("client.py -f <filename> -s <server> -p <port> -t <drone_type> -d <drone_serial>")
        sys.exit(2)

    ws = websocket.WebSocketApp("ws://" + server + ":" + port,
                              on_message = on_message,
                              on_error = on_error,
                              on_close = on_close)
    ws.on_open = on_open
    ws.run_forever()


if __name__ == "__main__":
    main(sys.argv[1:])