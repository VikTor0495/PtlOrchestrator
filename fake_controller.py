import socket

HOST = "127.0.0.1"
PORT = 5000

print(f"Fake PTL Controller listening on {HOST}:{PORT}")

with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
    s.bind((HOST, PORT))
    s.listen(1)

    conn, addr = s.accept()
    with conn:
        print(f"Connected by {addr}")
        while True:
            data = conn.recv(4096)
            if not data:
                print("Client disconnected")
                break

            print(f"RECEIVED: {data.hex()}")

            # Risposta PTL minimale valida
            response = b'\x02' + b'000002' + b'OK' + b'\r\n' + b'\x03'
            print(f"SENT: {response.hex()}")

            conn.sendall(response)
