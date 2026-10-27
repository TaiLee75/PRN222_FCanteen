// signalr-client.js - Client SignalR chạy trực tiếp trên trình duyệt
// Giúp phản hồi ngắt kết nối NGAY LẬP TỨC khi đóng tab (qua sự kiện beforeunload và WebSocket termination)

window.signalRHelper = {
    connection: null,
    dotNetHelper: null,

    start: function (dotNetHelper) {
        this.dotNetHelper = dotNetHelper;

        // Nếu đã có kết nối cũ thì ngắt trước
        if (this.connection) {
            try { this.connection.stop(); } catch (e) { }
        }

        // Tạo kết nối WebSocket trực tiếp từ trình duyệt đến OrderHub
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/orderHub")
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .build();

        // Đăng ký nhận sự kiện từ Hub
        this.connection.on("UpdateConnectionCount", (count) => {
            console.log("[SignalR JS] UpdateConnectionCount:", count);
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync("OnUpdateConnectionCount", count);
            }
        });

        this.connection.on("OrderCreated", (order) => {
            console.log("[SignalR JS] OrderCreated:", order);
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync("OnOrderCreated", order);
            }
        });

        this.connection.on("OrderStatusChanged", (status) => {
            console.log("[SignalR JS] OrderStatusChanged:", status);
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync("OnOrderStatusChanged", status);
            }
        });

        this.connection.on("MenuItemSoldOut", (id, name) => {
            console.log("[SignalR JS] MenuItemSoldOut:", id, name);
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync("OnMenuItemSoldOut", id, name);
            }
        });

        // Lab 08: Nhận sự kiện cảnh báo tồn kho thấp từ Worker Service
        this.connection.on("LowStockAlert", (alert) => {
            console.log("[SignalR JS] LowStockAlert:", alert);
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync("OnLowStockAlert", alert);
            }
        });

        // BẮT BUỘC: Khi người dùng tắt tab hoặc reload, lập tức gửi lệnh ngắt kết nối WebSocket
        // Giúp server trừ số thiết bị kết nối ngay trong 0.1 giây!
        window.addEventListener("beforeunload", () => {
            if (this.connection) {
                this.connection.stop();
            }
        });

        // Khởi động kết nối
        this.connection.start()
            .then(() => {
                console.log("[SignalR JS] Connected! ConnectionId:", this.connection.connectionId);
                if (this.dotNetHelper) {
                    this.dotNetHelper.invokeMethodAsync("OnConnected", this.connection.connectionId);
                }
            })
            .catch(err => {
                console.error("[SignalR JS] Connection error:", err);
            });
    },

    joinGroup: function (methodName, param) {
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
            this.connection.invoke(methodName, param)
                .catch(err => console.error(`[SignalR JS] Error in ${methodName}:`, err));
        }
    },

    leaveGroup: function (methodName, param) {
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
            this.connection.invoke(methodName, param)
                .catch(err => console.error(`[SignalR JS] Error in ${methodName}:`, err));
        }
    },

    stop: function () {
        if (this.connection) {
            this.connection.stop();
        }
    }
};
