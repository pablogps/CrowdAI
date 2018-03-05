
var actionUrl = null;
var intervalMilliseconds = 20000;

function LaunchHeartBeat(url) {
    actionUrl = url;
    setInterval(HeartBeat, intervalMilliseconds);
}

function HeartBeat() {  
    $.ajax({
        type: "POST",
        url: actionUrl
    });
}
