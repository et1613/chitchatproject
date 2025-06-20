let selectedReceiver = null;
const backendUrl = "https://localhost:5001";

// Eğer giriş yapılmamışsa sahte kullanıcı ile devam et
const fallbackUser = JSON.parse(localStorage.getItem("user")) || {
    name: "Ziyaretçi",
    email: "anonim@example.com",
    gender: "unknown",
    avatar: ""
};

// Bu fonksiyon sohbet sayfasında çalışır
function loadUser() {

  const token = localStorage.getItem("token");
  if (!token) {
    window.location.href = "index.html";
    return;
  }

  fetch(`${backendUrl}/api/me`, {
    headers: { Authorization: "Bearer " + token }
  })
    .then(res => {
      if (!res.ok) throw new Error("Token geçersiz");
      return res.json();
    })
    .then(data => {
      console.log("Kullanıcı:", data); // ✅ burada email: xxx döner
      // İstersen localStorage.setItem("userEmail", data.email); şeklinde de saklayabilirsin
    })
    .catch(() => {
      localStorage.removeItem("token");
      window.location.href = "index.html";
    });

  const userData = localStorage.getItem("chitchat_user");
if (!userData) {
  window.location.href = "index.html";
  return;
}
const user = JSON.parse(userData);

  $("#profileName").text(user.name);
  $("#profileGender").text(user.gender === "male" ? "Erkek" : "Kadın");
 const avatarPath = user.avatar || (user.gender === "male" ? "img/avatar_male.png" : "img/avatar_female.png");
$("#profileAvatar").attr("src", avatarPath);
 $("#btnProfile").click(function () {
  $("#chatSection").removeClass("active").hide();
  $("#profileSection").addClass("active").fadeIn(200);
  $("#btnChat").removeClass("active");
  $(this).addClass("active");
});

$("#btnChat").click(function () {
  $("#profileSection").removeClass("active").hide();
  $("#chatSection").addClass("active").fadeIn(200);
  $("#btnProfile").removeClass("active");
  $(this).addClass("active");
});

renderUserList(); // kullanıcıları yükle
  loadMessages();
  // Kullanıcı listesini göster


}

function renderUserList(filter = "") {
  const friends = JSON.parse(localStorage.getItem("chitchat_friends")) || [];
  const groups = JSON.parse(localStorage.getItem("chitchat_groups")) || [];
  const filtered = friends.filter(u =>
    u.name.toLowerCase().includes(filter.toLowerCase())
  );
  const blocked = JSON.parse(localStorage.getItem("chitchat_blocked")) || [];
const visible = filtered.filter(u => !blocked.includes(u.email));
let html = ""; // <-- yukarı taşı
if (visible.length === 0) {
  html = `<li class="list-group-item text-muted">Kullanıcı bulunamadı</li>`;
} else {
  visible.forEach(u => {
    // ...
  });
}
  // ✅ Grupları önce listele
groups.forEach(g => {
  html += `
    <li class="list-group-item d-flex align-items-center user-item group-item" data-name="${g.name}" data-email="group:${g.name}">
      <strong>👥 ${g.name}</strong>
    </li>
  `;
});
  if (filtered.length === 0) {
    html = `<li class="list-group-item text-muted">Kullanıcı bulunamadı</li>`;
  } else {
    filtered.forEach(u => {
  const avatar = u.avatar || (u.gender === "male" ? "img/avatar_male.png" : "img/avatar_female.png");
  html += `
    <li class="list-group-item d-flex align-items-center user-item" data-name="${u.name}" data-email="${u.email}">
      <img src="${avatar}" class="rounded-circle me-2" style="width:30px; height:30px;">
      ${u.name}
      <button class="btn btn-sm btn-outline-danger ms-auto" onclick="blockUser('${u.email}')">Engelle</button>
    </li>
  `;
});
  }

  document.getElementById("userList").innerHTML = html;

  document.querySelectorAll(".user-item").forEach(item => {
    item.addEventListener("click", () => {
      document.querySelectorAll(".user-item").forEach(i => i.classList.remove("active-user"));
      item.classList.add("active-user");

      selectedReceiver = {
  name: item.dataset.name,
  email: item.dataset.email
};


if (item.classList.contains("group-item")) {
  document.querySelector("#chatSection .card-header").innerText = `👥 Grup: ${selectedReceiver.name}`;
} else {
  document.querySelector("#chatSection .card-header").innerText = `${selectedReceiver.name} ile Sohbet`;
}
      loadMessages();
    });
  });
}
function blockUser(email) {
  let blocked = JSON.parse(localStorage.getItem("chitchat_blocked")) || [];
  if (!blocked.includes(email)) {
    blocked.push(email);
    localStorage.setItem("chitchat_blocked", JSON.stringify(blocked));
    alert("🚫 Kullanıcı engellendi.");
    renderUserList(); // listeyi güncelle
  }
}
function renderBlockedUsers() {
  const blocked = JSON.parse(localStorage.getItem("chitchat_blocked")) || [];
  const friends = JSON.parse(localStorage.getItem("chitchat_friends")) || [];
  const list = document.getElementById("blockedList");

  if (!list) return;

  list.innerHTML = "";

  if (blocked.length === 0) {
    list.innerHTML = `<li class="list-group-item text-muted">Engellenmiş kullanıcı yok</li>`;
    return;
  }

  blocked.forEach(email => {
    const user = friends.find(f => f.email === email) || { name: email };
    const item = document.createElement("li");
    item.className = "list-group-item d-flex justify-content-between align-items-center";
    item.innerHTML = `
      <span>${user.name} (${email})</span>
      <button class="btn btn-sm btn-outline-secondary" onclick="unblockUser('${email}')">Kaldır</button>
    `;
    list.appendChild(item);
  });
}
function unblockUser(email) {
  let blocked = JSON.parse(localStorage.getItem("chitchat_blocked")) || [];
  blocked = blocked.filter(e => e !== email);
  localStorage.setItem("chitchat_blocked", JSON.stringify(blocked));
  alert("✅ Kullanıcının engeli kaldırıldı.");
  renderUserList();
  renderBlockedUsers(); // listeyi yenile
}


// Arama kutusu olayı
document.addEventListener("DOMContentLoaded", () => {
  const searchInput = document.getElementById("userSearch");
  if (searchInput) {
    searchInput.addEventListener("input", (e) => {
      renderUserList(e.target.value);
    });
  } 
});
function populateFriendCheckboxes() {
  const friends = JSON.parse(localStorage.getItem("chitchat_friends")) || [];
  const container = document.getElementById("friendCheckboxes");
  container.innerHTML = "";

  if (friends.length === 0) {
    container.innerHTML = `<div class="text-muted">Hiç arkadaşınız yok.</div>`;
    return;
  }

  friends.forEach(friend => {
    container.innerHTML += `
      <div class="form-check">
        <input class="form-check-input" type="checkbox" value="${friend.email}" id="friend-${friend.email}">
        <label class="form-check-label" for="friend-${friend.email}">
          ${friend.name} (${friend.email})
        </label>
      </div>
    `;
  });
}

// ✅ DOĞRU: Fonksiyon DOMContentLoaded'ın dışına alınmalı
async function createGroup() {
  const token = localStorage.getItem("token");
  if (!token) return alert("Giriş yapmalısınız!");
  const groupName = document.getElementById("groupName").value.trim();
  const checkboxes = document.querySelectorAll("#friendCheckboxes input[type=checkbox]:checked");
  const memberEmails = Array.from(checkboxes).map(cb => cb.value);
  if (!groupName || memberEmails.length === 0) return alert("Grup adı ve en az bir üye seçmelisiniz.");
  try {
    const res = await fetch(`${backendUrl}/api/chat/rooms`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: "Bearer " + token
      },
      body: JSON.stringify({ name: groupName, members: memberEmails })
    });
    if (!res.ok) throw new Error("Grup oluşturulamadı");
    alert("✅ Grup oluşturuldu!");
    // Gerekirse grupları yeniden yükle
    // renderUserList();
    document.getElementById("groupName").value = "";
    $("#groupModal").modal("hide");
  } catch (err) {
    alert("❌ Hata: " + err.message);
  }
}

// MESAJ GÖNDERME (API ENTEGRASYONU)
async function sendMessage() {
  const token = localStorage.getItem("token");
  if (!token) return alert("Giriş yapmalısınız!");
  if (!selectedReceiver) return alert("Lütfen bir kişi veya grup seçin.");
  const messageInput = document.getElementById("messageInput");
  const content = messageInput.value.trim();
  if (!content) return;

  // Grup mu bireysel mi kontrolü
  let endpoint = "";
  let body = {};
  if (selectedReceiver.email.startsWith("group:")) {
    // Grup mesajı
    const chatRoomId = selectedReceiver.name;
    endpoint = `${backendUrl}/api/message/send`;
    body = { chatRoomId, content };
  } else {
    // Direkt mesaj
    endpoint = `${backendUrl}/api/chat/direct-messages`;
    body = { receiverId: selectedReceiver.email, content };
  }

  try {
    const res = await fetch(endpoint, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: "Bearer " + token
      },
      body: JSON.stringify(body)
    });
    if (!res.ok) throw new Error("Mesaj gönderilemedi");
    messageInput.value = "";
    loadMessages();
  } catch (err) {
    alert("❌ Hata: " + err.message);
  }
}

// MESAJLARI ÇEKME (API ENTEGRASYONU)
async function loadMessages() {
  const token = localStorage.getItem("token");
  if (!token) return;
  if (!selectedReceiver) return;
  let endpoint = "";
  if (selectedReceiver.email.startsWith("group:")) {
    // Grup mesajları
    const chatRoomId = selectedReceiver.name;
    endpoint = `${backendUrl}/api/message/chat/${chatRoomId}`;
  } else {
    // Direkt mesajlar (örnek: iki kullanıcının ortak chatRoomId'si olmalı)
    // Burada backend'den iki kullanıcı arasındaki chatRoomId'yi bulmak gerekebilir
    // Şimdilik email ile deniyoruz (gerekirse backend'e ek endpoint eklenir)
    endpoint = `${backendUrl}/api/chat/rooms/${selectedReceiver.email}/messages`;
  }
  try {
    const res = await fetch(endpoint, {
      headers: { Authorization: "Bearer " + token }
    });
    if (!res.ok) throw new Error("Mesajlar alınamadı");
    const messages = await res.json();
    renderMessages(messages);
  } catch (err) {
    document.getElementById("messageArea").innerHTML = `<div class='text-danger'>${err.message}</div>`;
  }
}

function renderMessages(messages) {
  const area = document.getElementById("messageArea");
  area.innerHTML = "";
  if (!messages || messages.length === 0) {
    area.innerHTML = `<div class='text-muted'>Hiç mesaj yok.</div>`;
    return;
  }
  messages.forEach(msg => {
    area.innerHTML += `<div><strong>${msg.senderName || msg.senderId}:</strong> ${msg.content}</div>`;
  });
  area.scrollTop = area.scrollHeight;
}

// Sohbet sayfası açıldığında çalışır
window.onload = function () {
  //const token = localStorage.getItem("token");
  const user = localStorage.getItem("chitchat_user");
   if (user) {
    const navUser = document.getElementById("navUserName");
    if (navUser) {
      navUser.innerText = `👋 ${user.name}`;
    }
  }

 // if (!token || !user) {
    // Token veya kullanıcı bilgisi yoksa giriş ekranına gönder
    //window.location.href = "index.html";
   // return;
  //}

  if (window.location.pathname.includes("chat.html")) {
    loadUser();
    document.getElementById("btnChat").click();
  }
   const searchInput = document.getElementById("userSearch");
  if (searchInput) {
    searchInput.addEventListener("input", (e) => {
      renderUserList(e.target.value);
    });
  }
};
function logout() {
  localStorage.removeItem("token");
  window.location.href = "index.html";
}
async function addFriend() {
  const token = localStorage.getItem("token");
  if (!token) return alert("Giriş yapmalısınız!");
  const email = document.getElementById("friendEmail").value.trim();
  if (!email) return alert("E-posta girin");
  try {
    const res = await fetch(`${backendUrl}/api/users/friends`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: "Bearer " + token
      },
      body: JSON.stringify({ email })
    });
    if (!res.ok) throw new Error("Arkadaş eklenemedi");
    alert("✅ Arkadaş eklendi!");
    document.getElementById("friendEmail").value = "";
    // Gerekirse arkadaş listesini güncelle
    // renderUserList();
  } catch (err) {
    alert("❌ Hata: " + err.message);
  }
}
function showNotification(message) {
  const notif = document.getElementById("notification");
  const notifText = document.getElementById("notificationText");
  notifText.innerText = message || "Yeni mesaj geldi!";
  notif.style.display = "block";

    // Sesli bildirim oynat
  const audio = document.getElementById("notifSound");
if (audio) {
  audio.currentTime = 0; // Baştan başlat
  audio.play().catch(err => {
    console.warn("Ses oynatılamadı:", err);
  });
}

  setTimeout(() => {
    notif.style.display = "none";
  }, 4000); // 4 saniye sonra gizle
}

function hideNotification() {
  document.getElementById("notification").style.display = "none";
}
function resetPassword() {
  const email = document.getElementById("resetEmail").value.trim();
  if (!email) {
    alert("Lütfen e-posta adresinizi girin.");
    return;
  }

  alert("📧 Şifre sıfırlama bağlantısı e-posta ile gönderilecek (simülasyon).");

  // Gerçek API'ye bağlamak istersen buraya POST isteği eklersin:
  // fetch('/api/reset-password', {
  //   method: 'POST',
  //   headers: { 'Content-Type': 'application/json' },
  //   body: JSON.stringify({ email })
  // });
}



